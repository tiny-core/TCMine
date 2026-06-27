# Monta a imagem do TCMine-Server a partir do binário JÁ COMPILADO e sobe via compose.
# O Dockerfile (single-stage) só copia ./publish — todo o build acontece aqui, no host.
#
# Uso:  ./build-image.ps1            (publica + sobe em foreground; Ctrl+C para parar)
#       ./build-image.ps1 -Detach   (publica + sobe em background)
param(
    [switch]$Detach
)

$ErrorActionPreference = 'Stop'

# Raiz do projeto = pasta deste script (independe de onde foi chamado)
Set-Location $PSScriptRoot

# 1/2 — Publish framework-dependent (portável): roda no runtime Linux da imagem mesmo a partir do
# Windows. Sem -r (RID) de propósito; -p:UseAppHost=false evita o .exe nativo inútil no container.
Write-Host '==> 1/2  Publicando TCMine-Server (Release, portável)...' -ForegroundColor Cyan
dotnet publish TCMine-Server/TCMine-Server.csproj -c Release -o publish -p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw "Publish falhou (exit $LASTEXITCODE)" }

# 2/2 — Constrói a imagem (só copia ./publish) e sobe os serviços do compose.
Write-Host '==> 2/2  Construindo a imagem e subindo o compose...' -ForegroundColor Cyan
if ($Detach) {
    docker compose up --build -d
} else {
    docker compose up --build
}
