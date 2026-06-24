---
type: entity
title: TCMine-Server
tags: [entity, tcmine, blazor, minimal-api, backend, admin]
status: wip
created: 2026-06-23
updated: 2026-06-24
aliases: [TCMine-Server, servidor, backend, painel admin]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
  - "[[sources/2026-06-24-modpack-admin-ui]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-design]]"
  - "[[entities/tcmine-launcher]]"
  - "[[concepts/setup-auth-cookie]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/modpack-admin-editor]]"
  - "[[concepts/sse-content-sync]]"
  - "[[concepts/player-config-sync]]"
---

# TCMine-Server

> ASP.NET Core **.NET 10**: Minimal API (backend que o launcher consome) +
> Blazor Server (painel admin com MudBlazor). É o ponto central do ecossistema.

## Visão geral

`TCMine-Server` (namespace `TCMine_Server`) serve o catálogo/manifesto de
modpacks e os jars ao launcher, faz proxy do CurseForge, sync de conteúdo via
SSE e sync de configs do jogador, e oferece a UI admin para gerir tudo.

## Responsabilidades / Escopo

- **Bootstrap (`Program.cs`):** Razor Components interativos + MudBlazor;
  `appsettings.local.json` + env; `ServerPaths.EnsureCreated`; `AddTcMineDatabase`
  + migrations no boot; **Data Protection** (chaves em `tcmine-data/secrets`,
  app name `TCMine-Server`); `ServerSettingsService` (singleton); HttpClient
  nomeado do CurseForge + `ICurseForgeApi`/`CurseForgeApiClient` (scoped);
  `ModpackImportService`; `MinecraftVersionService`/`MinecraftAuthService`;
  `UserService` + `SetupState`; **cookie auth** (`tcmine_auth`, 7 dias, sliding,
  `/login`, `/auth/logout`, AccessDenied → `/admin`); `ContentCatalog`/
  `SystemMetricsService`/`LauncherFeedService`/`ContentNotifier` (singletons);
  `MemoryCache`; **rate limiter** por IP (política `configs`, 30/min).
- **Pipeline:** exception handler → `/Error` (prod); **middleware de 404
  estilizado** (a catch-all `NotFound.razor` renderiza o corpo a 200 e marca o
  `HttpContext`; o middleware faz buffer e promove a 404 — API/SSE/assets ficam de
  fora); HTTPS redirect; static files de `/updates` (feed Velopack); rate limiter;
  auth/authorization/antiforgery; **middleware de primeira execução** (sem usuário
  → `/setup`); `MapRazorComponents<App>` interativo; `/auth/logout`.
- **Endpoints (Minimal API):** `MapModpackEndpoints` (catálogo + manifesto +
  serving de jars/overrides), `MapCurseForgeProxy` (`/v1/*` — ver
  [[concepts/curseforge-proxy]]), `MapEventsEndpoint` (`/events`, SSE — ver
  [[concepts/sse-content-sync]]), `MapPlayerConfigEndpoints`
  (`/players/{uuid}/configs/{modpackId}` — ver [[concepts/player-config-sync]]),
  `MapLauncherFeedEndpoints` (download/feed).
- **Authentication (`Authentication/`):** `AuthClaims`,
  `PersistingAuthenticationStateProvider` (identidade do cookie persistida do
  prerender para o circuito).
- **Components (Blazor):** layouts (`RootLayout`/`AdminLayout`/`PublicLayout`/
  `MainLayout`); páginas `Home`, `Login`, `Setup`, `Error`, `NotFound`,
  `Admin/Dashboard` (+ widgets: `DashboardKpis`, `SystemStatusCard`,
  `RecentActivityCard`, `RecentModpacksCard`, `ModDistributionCard`,
  `DashboardHeader`), `Admin/Settings` (token CF + Azure ids + `PublicBaseUrl`);
  `Admin/Modpacks/` (lista + editor em abas + diálogos — ver
  [[concepts/modpack-admin-editor]]); shared (`StatCard`, `CenterScreen`,
  `ErrorScreen`, `RelativeTime`).
- **UI — pacotes:** MudBlazor (admin) + **BlazorMonaco** (editor de overrides;
  versão central, 3 scripts de setup no `App.razor`).
- **Theme (`Theme/`):** `MudThemeFactory.Create()` monta o `MudTheme`
  (PaletteDark + PaletteLight, radius 8px, fonte Inter) a partir de
  [[entities/tcmine-design]].

## Decisões e estado atual

- **[2026-06-23]** Auth por **cookie** + `PersistingAuthenticationStateProvider`;
  primeira execução força `/setup` do `Owner`. Ver [[concepts/setup-auth-cookie]].
- **[2026-06-23]** `Admin/Settings` segue **escrita-só-ao-Guardar**; é onde o
  token CurseForge e os ids Azure são configurados (cifrados via
  `ServerSettingsService`). Hoje qualquer admin autenticado acede; restrição ao
  Owner é pendência.
- **[2026-06-23]** **404 estilizado** via render a 200 + promoção por middleware
  (rotas de API/SSE/assets excluídas).
- **[2026-06-24]** **UI admin de modpacks** entregue (`Admin/Modpacks/`): lista +
  editor em abas (Detalhes/Mods/Overrides/Servidores) sobre o
  `ModpackImportService`, com busca/import CurseForge, upload de jar, marcação de
  `Side`/`Target` por mod e edição de overrides com **BlazorMonaco**. Restrito a
  `Owner,Admin`. Ver [[concepts/modpack-admin-editor]].

## Relações

- Depende de [[entities/tcmine-infrastructure]], [[entities/tcmine-application]],
  [[entities/tcmine-design]]. Serve o [[entities/tcmine-launcher]].

## Pontos em aberto

- [x] CRUD admin de **modpacks** (2026-06-24). Falta CRUD de usuários/releases.
- [ ] Restringir `Settings` ao papel `Owner`.
- [x] **Newsletter por modpack** (2026-06-24) — `NewsEntity.ModpackId` (FK
  opcional, null = global) + migration `NewsModpackFk` nos dois providers + aba
  Novidades. Falta UI para o feed global. Ver [[concepts/modpack-admin-editor]].
- [ ] Orquestração de instâncias de servidor Minecraft (a partir do modpack).

## Referências

- Código: `TCMine-Server/Program.cs`, `Endpoints/`, `Authentication/`,
  `Components/`, `Theme/MudThemeFactory.cs`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
