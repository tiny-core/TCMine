param([Parameter(Mandatory)][string]$Nome)

$ErrorActionPreference = "Stop"

foreach ($provider in @("Postgres", "Sqlite"))
{
    $projeto = "src/server/TCMine.Server.Infrastructure.$provider"
    Write-Host "==> $provider"
    dotnet ef migrations add $Nome `
        --project $projeto `
        --startup-project $projeto `
        --context TCMineDbContext
}