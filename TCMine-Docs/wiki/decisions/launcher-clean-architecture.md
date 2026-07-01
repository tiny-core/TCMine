---
type: decision
title: Clean Architecture do launcher (projeto de infra dedicado)
tags: [decision, launcher, clean-architecture, arquitetura]
status: aceita
created: 2026-06-29
updated: 2026-06-29
deciders: [Jocian]
supersedes: []
superseded-by: []
sources:
  - "[[sources/2026-06-29-launcher-clean-architecture]]"
related:
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-launcher-infrastructure]]"
  - "[[entities/tcmine-solution]]"
  - "[[concepts/clean-architecture]]"
---

# Clean Architecture do launcher (projeto de infra dedicado)

> O launcher deixa de ter os serviços/models dentro do projeto de UI: passa a **espelhar o servidor** —
> `TCMine-Domain` (models) ← `TCMine-Application` (portas) ← **`TCMine-Launcher.Infrastructure`** (impls)
> ← `TCMine-Launcher` (só UI + composição).

## Contexto

Numa primeira implementação, todo o pipeline do launcher (download, CmlLib, filesystem, MSAL) vivia
**dentro do `TCMine-Launcher`** (pasta `Services/`). O usuário pediu que o launcher seguisse
[[concepts/clean-architecture]] como o servidor, alocando componentes/serviços nos projetos da solução.

A infra do launcher **não pode** ir para a infra do servidor (`TCMine-Server.Infrastructure`): isso
arrastaria **CmlLib** para o servidor e **EF Core/Docker** para o launcher (acoplamento cruzado entre os
dois apps).

> **[2026-07-01]** A infra do servidor, antes chamada `TCMine-Infrastructure`, foi **renomeada para
> `TCMine-Server.Infrastructure`** — ficando simétrica com a do launcher e deixando explícito no nome que é
> específica do servidor (não compartilhada). Reforça esta decisão. Ver [[entities/tcmine-server-infrastructure]].

## Decisão

Criar um **projeto de infraestrutura dedicado**, `TCMine-Launcher.Infrastructure`, e distribuir:

- **`TCMine-Domain/Launcher/`** — models puros: `InstalledModpack`, `ModpackServer`, `PlayerSession`,
  `LaunchProgress`, `LauncherSettings`, `PlayerDataProfile`. Sem dependências externas.
- **`TCMine-Application/Launcher/`** — **portas**: `IModpackCatalog`, `IAuthService`, `IInstanceStore`,
  `ISettingsStore`, `ILaunchOrchestrator`, `IGameRunStateStore`, `IServerPinger`, `ISystemInfo`
  (+ `ServerPing`/`RunState`).
- **`TCMine-Launcher.Infrastructure`** — implementações: `ModpackCatalog`/`AuthService` (MSAL),
  `InstanceStore`/`SettingsStore`/`GameRunStateStore` (filesystem), `LaunchOrchestrator` + colaboradores
  internos (`GameLauncher` NeoForge, `ModInstaller`, `OverridesInstaller`, `ServersDatWriter`,
  `GameLogCapture`), `ServerPinger`, `SystemInfo`, `ServerConfig`/`AppConfig`/`HttpClientProvider`/
  `LauncherPaths`. Referencia Domain + Application + CmlLib/XboxAuthNet/fNbt.
- **`TCMine-Launcher`** — só **Views/ViewModels** + composição (Splat) + behaviors de UI (`ImageLoader`).
  Os ViewModels dependem **só das portas**; a composição (root no `Program.cs`) instancia as impls.

## Consequências

- **Positivas:** isolamento total entre servidor e launcher (CmlLib não entra no servidor; EF/Docker não
  entram no launcher); ViewModels testáveis contra portas; mesma forma do servidor
  (Domain←Application←Infrastructure). A solução passa a **8 projetos**.
- **Negativas / custos:** mais um projeto e indireção de portas; o `MSession` (CmlLib) fica encapsulado
  na infra — o `AuthService` devolve um `PlayerSession` de domínio e expõe o `MSession` (internal) só ao
  `LaunchOrchestrator` (ambos infra), evitando vazar o tipo CmlLib para a apresentação.

## Alternativas consideradas

- **Reusar projetos, infra dentro do `TCMine-Launcher`** — menos projetos, mas mistura
  infraestrutura + apresentação num só.
- **Infra do launcher dentro da infra do servidor (`TCMine-Server.Infrastructure`)** — rejeitada (acopla CmlLib ao servidor e EF/Docker ao
  launcher).

## Referências

- [[entities/tcmine-launcher-infrastructure]], [[entities/tcmine-launcher]].
- Fonte: [[sources/2026-06-29-launcher-clean-architecture]].
