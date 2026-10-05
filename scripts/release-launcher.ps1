<#
.SYNOPSIS
    Publica e empacota o TCMine Launcher (self-contained, win-x64, Velopack).

.DESCRIPTION
    Substitui o antigo workflow release-launcher.yml, que saiu do GitHub Actions
    (ver CLAUDE.md §12 e docs/RELEASE.md): compilar/empacotar um WPF só faz
    sentido na máquina onde ele vai rodar. Isto é esse mesmo processo, para
    rodar daqui — do terminal do Rider, por exemplo.

    A versão NÃO é parâmetro: vem de src/launcher/VERSION, a mesma que a
    imagem do servidor embute. Para lançar uma versão nova, suba o número
    nesse arquivo e comite — é o que o workflow de release do servidor confere.

.PARAMETER SkipTests
    Pula a suíte antes de publicar. Um launcher publicado é tão imutável
    quanto uma imagem Docker — alguém pode já ter instalado no minuto
    seguinte —, então isto é para reexecuções rápidas com código já testado,
    não para o caminho normal.

.EXAMPLE
    ./scripts/release-launcher.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$SdkVersion = (Get-Content "$Root\global.json" | ConvertFrom-Json).sdk.version

$Version = (Get-Content "$Root\src\launcher\VERSION" -Raw).Trim()
if (-not $Version) { throw "src\launcher\VERSION está vazio." }

# O dotnet do PATH pode resolver para o SDK errado quando o exigido pelo
# global.json só está instalado em %USERPROFILE%\.dotnet (comum quando o
# instalador da máquina só trouxe uma versão mais velha para Program Files).
# Sem isto, o publish falha com "SDK not found" antes de chegar perto do
# launcher — mesma classe de armadilha que o CLAUDE.md documenta para o
# dotnet ef sob WSL, só que aqui é Windows puro.
# Só redireciona STDOUT (">", não "2>"): no PowerShell 5.1, redirecionar o
# stderr de um comando nativo embrulha cada linha num NativeCommandError, e
# com $ErrorActionPreference = "Stop" isso vira exceção ali mesmo — antes de
# $LASTEXITCODE existir para o "if" de baixo decidir alguma coisa. Foi assim
# que este bloco quebrava exatamente na primeira SDK ausente que deveria tratar.
dotnet --version > $null
if ($LASTEXITCODE -ne 0) {
    if (Test-Path "$env:USERPROFILE\.dotnet") {
        Write-Host "SDK $SdkVersion não resolvido no PATH padrão; usando $env:USERPROFILE\.dotnet"
        $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
        $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
        dotnet --version > $null
    }

    if ($LASTEXITCODE -ne 0) {
        throw "SDK $SdkVersion (do global.json) não encontrado. Instale-o ou ajuste o PATH."
    }
}

# O canal do Velopack deriva do PROTOCOLO, não da versão do produto — dois
# lugares com o mesmo número acabam a discordar, e publicar no canal errado
# entrega uma atualização que o servidor nunca vai oferecer.
$protocolMatch = Select-String -Path "$Root\src\shared\TCMine.Contracts\Protocol.cs" -Pattern 'Current\s*=\s*(\d+)'
if (-not $protocolMatch) { throw "Não consegui ler Protocol.Current em Protocol.cs." }
$protocol = $protocolMatch.Matches[0].Groups[1].Value
$channel = "win-x64-p$protocol"

Write-Host "Versão $Version, canal $channel"

if (-not $SkipTests) {
    # Só as suítes do launcher: um release do launcher não toca em código do
    # servidor, e rodar as 600+ provas do servidor aqui seria pagar minutos
    # por um veredito que o CI (ci.yml, a cada push) já deu.
    Write-Host "==> Testes"
    foreach ($suite in "TCMine.Launcher.Core.Tests", "TCMine.Launcher.Architecture.Tests") {
        dotnet run --project "$Root\tests\$suite" -c Release
    }
}

# Fora do repositório, de propósito: isto é só o intermediário que o vpk
# consome no passo seguinte — ninguém volta a olhar para ele depois, e um
# publish self-contained passa de 100 MB de DLLs do runtime. Deixá-lo dentro
# do repo era a pasta "publicado/" aparecendo como untracked a cada release,
# um passo de "git add -A" longe de ir parar num commit.
$publishDir = Join-Path ([System.IO.Path]::GetTempPath()) "tcmine-launcher-publish"

# Limpa antes: um publish self-contained não apaga sozinho o que sobrou de uma
# versão anterior (um arquivo removido entre uma versão e outra ficaria para
# trás), e o Velopack de qualquer forma substitui a pasta inteira.
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "==> Publicar (self-contained, win-x64)"
dotnet publish "$Root\src\launcher\TCMine.Launcher.App" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

# A versão do vpk (CLI) tem de acompanhar o pacote Velopack referenciado pelo
# launcher (Directory.Packages.props) — um vpk mais novo ou mais velho pode
# empacotar num formato que a biblioteca embutida no launcher não entende, e
# o sintoma só aparece no autoupdate, não aqui.
$velopackVersion = (Select-String -Path "$Root\Directory.Packages.props" `
    -Pattern 'PackageVersion Include="Velopack" Version="([^"]+)"').Matches[0].Groups[1].Value

$vpkInstalado = dotnet tool list -g | Select-String -SimpleMatch "vpk "

if (-not $vpkInstalado) {
    Write-Host "==> Instalando vpk $velopackVersion"
    dotnet tool install -g vpk --version $velopackVersion
}
elseif ($vpkInstalado -notmatch [regex]::Escape($velopackVersion)) {
    Write-Host "==> Atualizando vpk para $velopackVersion (versão do pacote Velopack do launcher)"
    dotnet tool update -g vpk --version $velopackVersion
}

$releasesDir = "$Root\releases\launcher"

Write-Host "==> Empacotar com o Velopack"
vpk pack `
    --packId TCMine.Launcher `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe TCMine.Launcher.App.exe `
    --packTitle "TCMine Launcher" `
    --channel $channel `
    --runtime win-x64 `
    --outputDir $releasesDir

if ($LASTEXITCODE -ne 0) { throw "vpk pack falhou." }

Write-Host ""
Write-Host "Pronto: $releasesDir"
Write-Host "Copie o conteúdo para `${TCMINE_ROOT}\updates\launcher\$channel\ no servidor."
Write-Host "O *-Setup.exe é o instalador para quem ainda não tem o launcher."
