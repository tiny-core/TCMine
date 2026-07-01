---
type: source
title: Compilação do launcher pelo servidor (Velopack)
tags: [source, code, launcher, velopack, build, releases]
status: ingested
created: 2026-07-01
updated: 2026-07-01
source-type: code
origin: "implementação a pedido do usuário"
feeds:
  - "[[concepts/launcher-build-velopack]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
---

# Compilação do launcher pelo servidor (Velopack)

Implementação da feature "o TCMine-Server compila o launcher". Escolhas confirmadas pelo usuário:
**página dedicada `/admin/releases`** + **diálogo de versão/notas**.

## Arquivos

- **Novo** `TCMine-Server.Infrastructure/Launcher/LauncherBuildService.cs` — job de fundo (singleton,
  progresso reconectável): lê `PublicBaseUrl`/`AzureClientId`; `dotnet publish` (self-contained, injeta
  `TcmineServerUrl`/`MicrosoftClientId`); `vpk pack`; grava `ReleaseEntity`. Streama a saída dos processos.
- **Novo** `TCMine-Server.Infrastructure/Launcher/ReleaseService.cs` — lista o histórico de releases.
- `TCMine-Server.Infrastructure/Server/ServerSettingsService.cs` — `GetPublicBaseUrlAsync()`.
- **Novo** `TCMine-Server/Components/Pages/Admin/Releases/Releases.razor(.cs)` + diálogo
  `Dialogs/LauncherBuildDialog.razor(.cs)`.
- `TCMine-Server/Components/Layout/AdminLayout.razor` — NavLink "Releases" ativado (antes desabilitado).
- `TCMine-Server/Program.cs` — registra `LauncherBuildService` (singleton) + `ReleaseService` (scoped).
- **Launcher:** `TCMine-Launcher/Program.cs` (`VelopackApp.Build().Run()`), `TCMine-Launcher.csproj`
  (`Velopack`), `Directory.Packages.props` (`Velopack 1.2.0`).

## Validação (real, ponta-a-ponta)

- Rodei o pipeline manualmente contra o launcher: **`dotnet publish` (exit 0)** + **`vpk pack` (exit 0)**
  geraram `RELEASES`, `TCMine-Launcher-1.0.0-full.nupkg`, `TCMine-Launcher-win-Setup.exe`,
  `releases.win.json`, `assets.win.json`, `-Portable.zip`.
- **Bug pego pelo teste:** a 1ª tentativa do `vpk pack` **falhou** ("Unable to verify VelopackApp is
  called") — faltava `VelopackApp.Build().Run()` no `Main` do launcher. Adicionado → passou.
- Solução compila 0/0; boot do server limpo (DI válido). A UI (atrás de login) não foi clicada.

## Pendências

- Consumidor no launcher (`UpdateManager` checando `/updates` + "atualizar agora" na UI).
- Assinatura de código (SmartScreen).
- Build em Docker/Linux runtime-only não é suportado (precisa SDK + fonte + vpk; Setup.exe do Windows a
  partir do Linux tem limitações).
