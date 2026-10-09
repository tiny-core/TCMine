<#
.SYNOPSIS
    Medições da linha de base (docs/BASELINE.md) na máquina de desenvolvimento.

.DESCRIPTION
    Versão para Windows do scripts/baseline.sh. Aquele mede a instalação de
    verdade, em Docker; este mede o servidor rodando por `dotnet run`, com o
    banco de dev. Serve para TESTAR o método e comparar antes/depois na mesma
    máquina — os números que vão para o BASELINE.md são os de produção.

    Não altera dados: o banco é aberto em modo somente leitura.

.EXAMPLE
    .\scripts\baseline.ps1 boot
    Três arranques, tempo até /health/live responder. Sobe e derruba o servidor.

.EXAMPLE
    .\scripts\baseline.ps1 mem
    Memória do servidor que já está rodando (pela IDE ou por dotnet run).

.EXAMPLE
    .\scripts\baseline.ps1 db
    Tamanho do banco de dev e linhas por tabela (exige Python, como o tc db).

.EXAMPLE
    .\scripts\baseline.ps1 report
    Lê o log do launcher INSTALADO e entrega amostras e medianas do arranque,
    mais as linhas de instalação/atualização de modpack. Este é o único comando
    cujo resultado vai para o BASELINE.md. Use -Days 3 para os últimos 3 dias.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("boot", "mem", "db", "report")]
    [string]$Command,

    [string]$Url = "http://localhost:5144",

    # Release por padrão: Debug mede o código sem otimização e engana.
    [string]$Configuration = "Release",

    [int]$Rounds = 3,

    # report: quantos arquivos de log (um por dia) entram na conta.
    [int]$Days = 1
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\server\TCMine.Server.Web"
$database = Join-Path $project "data\tcmine.db"
$processName = "TCMine.Server.Web"

function Test-Live {
    try {
        $response = Invoke-WebRequest -Uri "$Url/health/live" -UseBasicParsing -TimeoutSec 1
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Stop-Server {
    Get-Process -Name $processName -ErrorAction SilentlyContinue | Stop-Process -Force
    # Stop-Process volta assim que PEDE a morte; a porta só fica livre quando o
    # processo sai de fato.
    while (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
        Start-Sleep -Milliseconds 100
    }
}

function Measure-Boot {
    if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
        throw "O servidor já está rodando. Pare-o antes (IDE ou scripts/tc kill): a medição precisa subir do zero."
    }

    # Compila fora do cronômetro; o arranque medido usa --no-build.
    Write-Host "Compilando ($Configuration)..."
    dotnet build $project -c $Configuration --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Build falhou." }

    $log = Join-Path $env:TEMP "tcmine-baseline-boot.log"

    for ($round = 1; $round -le $Rounds; $round++) {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()

        $run = Start-Process dotnet -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput $log `
            -ArgumentList "run", "--project", "`"$project`"", "-c", $Configuration,
                          "--no-build", "--launch-profile", "http"

        # /health/live e não /health: o live responde assim que o Kestrel
        # escuta; o /health só passa com o banco pronto e mediria outra coisa.
        while (-not (Test-Live)) {
            if ($run.HasExited) {
                throw "O servidor saiu antes de responder. Veja $log"
            }
            if ($watch.Elapsed.TotalSeconds -gt 120) {
                Stop-Server
                throw "Sem resposta em 120 s. Veja $log"
            }
            Start-Sleep -Milliseconds 50
        }

        Write-Host ("boot {0}: {1} ms" -f $round, $watch.ElapsedMilliseconds)
        Stop-Server
    }

    Write-Host "Inclui cerca de 1 s do próprio 'dotnet run'; compare só com outras medições feitas por este script."
}

function Measure-Memory {
    $process = Get-Process -Name $processName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $process) {
        throw "O servidor não está rodando. Suba-o e deixe em repouso por um minuto antes de medir."
    }

    Write-Host ("memória (working set): {0:N0} MB" -f ($process.WorkingSet64 / 1MB))
    Write-Host ("memória (privada):     {0:N0} MB" -f ($process.PrivateMemorySize64 / 1MB))
}

function Measure-Database {
    if (-not (Test-Path $database)) { throw "Banco de dev não encontrado em $database" }

    $python = Get-Command python, py -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $python) { throw "Python não encontrado no PATH (é o mesmo que o 'tc db' usa)." }

    $bytes = (Get-Item $database).Length
    $wal = "$database-wal"
    if (Test-Path $wal) { $bytes += (Get-Item $wal).Length }
    Write-Host ("tamanho (db + wal): {0:N1} MB" -f ($bytes / 1MB))

    # Num arquivo temporário e não em 'python -c': aspas dentro de aspas na
    # linha de comando do Windows são uma loteria.
    $script = Join-Path $env:TEMP "tcmine-baseline-db.py"
    @'
import sqlite3, sys
con = sqlite3.connect("file:///" + sys.argv[1].replace("\\", "/") + "?mode=ro", uri=True)
tables = [r[0] for r in con.execute(
    "select name from sqlite_master where type='table' and name not like 'sqlite_%' order by name")]
rows = sorted(((con.execute('select count(*) from "%s"' % t).fetchone()[0], t) for t in tables), reverse=True)
for count, table in rows:
    print("%-32s %d" % (table, count))
'@ | Set-Content -Path $script -Encoding ASCII

    & $python.Source $script $database
}

function Get-Median {
    param([double[]]$Values)

    $sorted = @($Values | Sort-Object)
    $count = $sorted.Count
    if ($count -eq 0) { return 0 }

    $middle = [int][math]::Floor($count / 2)
    if ($count % 2 -eq 1) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

function Show-Report {
    $logs = Join-Path $env:LOCALAPPDATA "TCMine\logs"
    if (-not (Test-Path $logs)) { throw "Pasta de logs do launcher não encontrada em $logs" }

    $files = @(Get-ChildItem -Path $logs -Filter "launcher-*.log" | Sort-Object Name | Select-Object -Last $Days)
    if ($files.Count -eq 0) { throw "Nenhum launcher-*.log em $logs" }

    # Ordenado: os marcos saem na ordem em que acontecem no arranque.
    $marks = New-Object System.Collections.Specialized.OrderedDictionary
    $installs = New-Object System.Collections.Generic.List[string]

    foreach ($file in $files) {
        # Get-Content e não File.ReadLines: o launcher aberto mantém o arquivo
        # em escrita, e o Get-Content lê assim mesmo.
        foreach ($line in (Get-Content -Path $file.FullName -Encoding UTF8)) {
            # O "." no lugar das letras acentuadas: a medição não pode depender
            # da codificação do console.
            if ($line -match 'Arranque: "?(.+?)"? em (\d+) ms(?: \("?([^")]+)"?\))?') {
                $name = $Matches[1]
                # O desfecho (com sessão, login, offline...) separa caminhos de
                # custo diferente, que não podem entrar na mesma mediana.
                if ($Matches[3]) { $name = "$name ($($Matches[3]))" }

                if (-not $marks.Contains($name)) {
                    $marks.Add($name, (New-Object System.Collections.Generic.List[double]))
                }
                $marks[$name].Add([double]$Matches[2])
            }
            elseif ($line -match '\] (Instala.+o de modpack .+)$') {
                $installs.Add($Matches[1])
            }
        }
    }

    Write-Host ("Arquivos: {0}" -f (($files | ForEach-Object { $_.Name }) -join ", "))

    if ($marks.Count -eq 0) {
        Write-Host "Nenhuma linha 'Arranque:' no período."
    }

    foreach ($name in $marks.Keys) {
        $values = $marks[$name].ToArray()
        Write-Host ("{0} ({1} aberturas)" -f $name, $values.Count)
        Write-Host ("  {0}  -> mediana {1:N0} ms" -f ($values -join ", "), (Get-Median $values))
    }

    if ($installs.Count -gt 0) {
        Write-Host ""
        Write-Host ("Instalações e atualizações ({0})" -f $installs.Count)
        foreach ($install in $installs) { Write-Host "  $install" }
    }

    Write-Host ""
    Write-Host "A primeira abertura depois de ligar o PC é a medição 'a frio'; as seguintes são 'a quente'."
}

switch ($Command) {
    "report" { Show-Report }
    "boot" { Measure-Boot }
    "mem"  { Measure-Memory }
    "db"   { Measure-Database }
    default { Get-Help $PSCommandPath -Examples }
}
