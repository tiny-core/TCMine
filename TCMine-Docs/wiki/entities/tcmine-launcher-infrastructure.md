---
type: entity
title: TCMine-Launcher.Infrastructure
tags: [entity, tcmine, launcher, infrastructure, cmllib]
status: wip
created: 2026-06-29
updated: 2026-07-05
aliases: [Launcher Infrastructure, infra do launcher]
sources:
  - "[[sources/2026-06-29-launcher-clean-architecture]]"
  - "[[sources/2026-07-05-launcher-infra-folders]]"
related:
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-domain]]"
  - "[[decisions/launcher-clean-architecture]]"
  - "[[concepts/launcher-install-launch]]"
---

# TCMine-Launcher.Infrastructure

> Camada de **infraestrutura do launcher**: implementa as portas (`TCMine-Application/Launcher`) com
> CmlLib (login MSAL + NeoForge), HTTP (catálogo/download), filesystem e fNbt (`servers.dat`). Espelha
> o `TCMine-Server.Infrastructure` do servidor, mas isolada (CmlLib não toca no servidor).

## Visão geral

Projeto criado em **2026-06-29** ao mover o pipeline do launcher para fora do projeto de UI (ver
[[decisions/launcher-clean-architecture]]). Referencia `TCMine-Domain` + `TCMine-Application` e os
pacotes CmlLib (`Core`, `Auth.Microsoft`, `Installer.NeoForge`), `XboxAuthNet.Game.Msal`, `fNbt`.

## Responsabilidades / Escopo

- **Login (`AuthService : IAuthService`):** MSAL + CmlLib; devolve `PlayerSession` (domínio) e mantém o
  `MSession` (CmlLib) **internal**, exposto só ao `LaunchOrchestrator`.
- **Catálogo (`ModpackCatalog : IModpackCatalog`):** HTTP contra `/api/modpacks` (+ ping).
- **Lançamento (`LaunchOrchestrator : ILaunchOrchestrator`):** conduz `GameLauncher` (NeoForge via
  CmlLib), `ModInstaller` (download do cache do servidor, `/files/...`, sem Sha1), `OverridesInstaller`
  (`/overrides.zip`) e `ServersDatWriter` (fNbt); anexa `GameLogCapture`. Ver
  [[concepts/launcher-install-launch]].
- **Persistência/estado:** `InstanceStore`, `SettingsStore`, `GameRunStateStore` (filesystem);
  `ServerPinger` (Server List Ping); `SystemInfo` (RAM física).
- **Config/infra de rede:** `ServerConfig`/`AppConfig` (URL e Azure id injetados no build),
  `HttpClientProvider`, `LauncherPaths` (`%AppData%/TCMine`).

## Organização (pastas)

Desde **2026-07-05** os arquivos deixaram a raiz e foram agrupados por **área de domínio**,
espelhando o `TCMine-Server.Infrastructure` (pastas com **namespace casando**:
`TCMine_Launcher.Infrastructure.<Pasta>`). Ver [[sources/2026-07-05-launcher-infra-folders]].

- **`Auth/`** — `AuthService` (login MSAL/CmlLib).
- **`Configuration/`** — `AppConfig`, `ServerConfig` (URL/Azure id injetados no build; resolução de URLs).
- **`Content/`** — `ModpackCatalog`, `NewsFeed`, `ContentWatcher` (catálogo/novidades/SSE do servidor).
- **`FileSystem/`** — `LauncherPaths` (`%AppData%/TCMine`).
- **`Launch/`** — pipeline de install/launch: `LaunchOrchestrator`, `GameLauncher`, `ModInstaller`,
  `OverridesInstaller`, `ServersDatWriter`, `GameLogCapture`, `PlayerConfigSync`.
- **`Networking/`** — `HttpClientProvider`, `ServerPinger`.
- **`Persistence/`** — `InstanceStore`, `SettingsStore`, `GameRunStateStore` (stores JSON em disco).
- **`Platform/`** — `SystemInfo` (RAM física). (Não `System/`, que colidiria com o namespace `System`.)
- **`Updates/`** — `UpdateService` (Velopack).

## Relações

- Implementa as portas de [[entities/tcmine-application]]; usa os models de [[entities/tcmine-domain]].
- Consumida (composição) por [[entities/tcmine-launcher]] (UI). Não é referenciada pelo servidor.

## Referências

- Código: `TCMine-Launcher.Infrastructure/<Pasta>/*.cs` (ver Organização acima).
- Fontes: [[sources/2026-06-29-launcher-clean-architecture]], [[sources/2026-07-05-launcher-infra-folders]].
