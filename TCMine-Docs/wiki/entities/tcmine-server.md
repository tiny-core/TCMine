---
type: entity
title: TCMine-Server
tags: [entity, tcmine, blazor, minimal-api, backend, admin]
status: wip
created: 2026-06-23
updated: 2026-07-05
aliases: [TCMine-Server, servidor, backend, painel admin]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
  - "[[sources/2026-06-24-modpack-admin-ui]]"
  - "[[sources/2026-07-01-dashboard-metrics-home]]"
  - "[[sources/2026-07-05-global-metrics-per-instance]]"
  - "[[sources/2026-07-05-player-configs-admin-hardening]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-server-infrastructure]]"
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
  `MapLauncherFeedEndpoints` (download/feed), `MapNewsEndpoints` (`/api/news` — feed público de
  novidades globais + de modpacks, só publicadas; consumido pela aba Novidades do launcher).
- **Authentication (`Authentication/`):** `AuthClaims`,
  `PersistingAuthenticationStateProvider` (identidade do cookie persistida do
  prerender para o circuito).
- **Components (Blazor):** layouts (`RootLayout`/`AdminLayout`/`PublicLayout`/
  `MainLayout`); páginas `Home`, `Login`, `Setup`, `Error`, `NotFound`,
  `Admin/Dashboard` (+ widgets: `DashboardKpis`, `SystemStatusCard`,
  `ServerInstancesMetricsCard`, `RecentActivityCard`, `RecentModpacksCard`,
  `ModDistributionCard`, `DashboardHeader`), `Admin/Settings` (token CF + Azure ids + `PublicBaseUrl`);
  `Admin/Modpacks/` (lista + editor em abas + diálogos — ver
  [[concepts/modpack-admin-editor]]); `Admin/Mods/` (catálogo de todos os arquivos
  de mod + badges dos modpacks + marcador/limpeza de órfãos — ver
  [[decisions/mods-many-to-many]]); `Admin/News/` (novidades globais + de modpacks
  num só lugar, seletor de modpack opcional no diálogo); `Admin/Players/` (gestão/
  limpeza das configs sincronizadas dos jogadores, agrupadas por jogador — ver
  [[concepts/player-config-sync]]); `Admin/Users/` (gestão de
  usuários + `UserEditDialog`, só Owner — ver [[concepts/setup-auth-cookie]]); shared
  (`StatCard`, `CenterScreen`, `ErrorScreen`, `RelativeTime`, `BusyOverlay` — ver
  [[concepts/async-feedback-overlay]]).
- **Services (`Services/`):** `BusyService` — estado de "ocupado" por circuito que
  alimenta o `BusyOverlay` no `RootLayout` (ver [[concepts/async-feedback-overlay]]).
- **UI — pacotes:** MudBlazor (admin) + **BlazorMonaco** (editor de overrides;
  versão central, 3 scripts de setup no `App.razor`).
- **Theme (`Theme/`):** `MudThemeFactory.Create()` monta o `MudTheme`
  (PaletteDark + PaletteLight, radius 8px, fonte Inter) a partir de
  [[entities/tcmine-design]].

## Decisões e estado atual

- **[2026-06-29]** O **login Microsoft do jogador NÃO é feito pelo servidor** — fica no launcher via
  MSAL (ver [[decisions/auth-msal-launcher]], que substituiu a tentativa server-brokered). O servidor
  só **valida** o token Minecraft quando precisa (sync de configs, `MinecraftAuthService`).
- **[2026-06-23]** Auth por **cookie** + `PersistingAuthenticationStateProvider`;
  primeira execução força `/setup` do `Owner`. Ver [[concepts/setup-auth-cookie]].
- **[2026-06-23]** `Admin/Settings` segue **escrita-só-ao-Guardar**; é onde o
  token CurseForge e os ids Azure são configurados (cifrados via
  `ServerSettingsService`). **[2026-06-25]** restrita ao `Owner` via
  `@attribute [Authorize(Roles = "Owner")]` (antes só o link do menu escondia; a
  página era acessível por URL).
- **[2026-06-23]** **404 estilizado** via render a 200 + promoção por middleware
  (rotas de API/SSE/assets excluídas).
- **[2026-06-24]** **UI admin de modpacks** entregue (`Admin/Modpacks/`): lista +
  editor em abas (Detalhes/Mods/Overrides/Servidores) sobre o
  `ModpackImportService`, com busca/import CurseForge, upload de jar, marcação de
  `Side`/`Target` por mod e edição de overrides com **BlazorMonaco**. Restrito a
  `Owner,Admin`. Ver [[concepts/modpack-admin-editor]].
- **[2026-06-25]** **Página de mods** (`/admin/mods`, Owner/Admin): lista todos os
  `ModFile` do servidor com badges dos modpacks em que aparecem, marca **órfãos**
  (`ModFileEntity.OrphanedAt`, mantido por `MarkOrphansAsync`) e permite apagá-los
  (`DeleteOrphanFileAsync`). Ver [[decisions/mods-many-to-many]].
- **[2026-06-25]** **Overlay bloqueante de feedback async**: `BusyService` (scoped)
  + `BusyOverlay` (no `RootLayout`) cobrem a tela com um modal não-fechável durante
  toda operação async do painel; skeletons das listas removidos. Convenção no
  `CLAUDE.md`. Ver [[concepts/async-feedback-overlay]].
- **[2026-06-25]** **Gestão de usuários** (`/admin/users`, só `Owner`): página
  `Admin/Users/Users` + `UserEditDialog` para criar/editar (login, papel, ativo,
  senha opcional na edição). `UserService` ganhou `UpdateAsync` e guardas reais do
  **último Owner ativo** em `Update`/`SetActive`/`Delete` (antes só havia a contagem,
  sem aplicá-la). Ver [[concepts/setup-auth-cookie]].
- **[2026-06-25]** **Dashboard refinado**: o card "Atividade recente"
  (`RecentActivityCard`) passa a exibir o **nome do modpack** em vez do `Guid`
  (subconsulta em `ContentCatalog`, pois `OverrideHistoryEntry` não tem navigation
  property; cai para o id quando o modpack já foi excluído). E o KPI único de
  "Novidades" (`DashboardKpis`) virou **dois**: *Novidades globais*
  (`News.ModpackId == null`) e *Novidades de modpacks* (`ModpackId != null`),
  ambos contando só publicadas, a partir do agregado `DashboardData`.

- **[2026-07-01]** **Dashboard com medidores de recurso + home pública revampada**:
  - `SystemMetricsService` (em `TCMine-Server.Infrastructure/Server`) deixou de medir só o
    processo e passou a capturar **uso de CPU, RAM e disco** do host/contêiner, de forma
    **cross-platform** (sem `PerformanceCounter`/WMI): CPU pelo delta de
    `Process.TotalProcessorTime` normalizado por núcleo; RAM via `GC.GetGCMemoryInfo()`
    (`MemoryLoadBytes`/`TotalAvailableMemoryBytes`, que **honram limites do Docker**); disco
    via `DriveInfo` do drive que hospeda `tcmine-data`. O serviço virou **stateful** (guarda a
    última amostra de CPU) e recebe o `dataRoot` no construtor — o registo passou a
    `AddSingleton(new SystemMetricsService(dataRoot))` no `Program.cs`. O `SystemSnapshot`
    agora expõe `CpuPercent`, `Ram`/`Disk` (`(Used,Total)`) e helpers de %/GB.
  - **`SystemStatusCard`** ganhou três **medidores circulares** (`MudProgressCircular`
    0–100) para CPU/RAM/Disco, via um novo componente reutilizável **`MetricGauge`**
    (razor + code-behind + css escopado; cor por limiar verde/atenção/crítico). Mantém os
    tiles do processo (working set/heap/threads/uptime) e o gráfico de histórico de memória.
  - **`ModDistributionCard`** trocou as barras lineares por um **donut** (cliente/servidor/
    ambos) + legenda; migrado para a API de gráficos do **MudBlazor 9** (`ChartSeries` +
    `ChartLabels`; `InputData`/`InputLabels` foram removidos na v9).
  - **Home pública (`Home.razor`)** deixou de ser só o hero: fez-se o **wire real** ao
    `ContentCatalog` (modpacks publicados + disponibilidade do launcher via feed Velopack),
    com hero (gradiente de acento), três cards de destaque e **grade de modpacks publicados**
    (versão/MC/loader/mods + nº de servidores). CSS escopado com `::deep` (num wrapper
    `.home-page`) porque o CSS escopado do Blazor não atinge elementos de componentes-filhos
    (MudPaper) sem `::deep`.

- **[2026-07-05]** **Métricas do sistema tornadas globais + card por instância** (a pedido do
  usuário — **substitui** a semântica de CPU/disco do item de 2026-07-01; ver
  [[sources/2026-07-05-global-metrics-per-instance]]):
  - `SystemMetricsService` reescrito: o medidor de **CPU** passou de só-processo para **global do
    host** (todos os núcleos) — no Linux lê os contadores acumulados de `/proc/stat` (que no
    contêiner refletem o host, pois o accounting de CPU não é isolado por namespace); no Windows,
    P/Invoke `GetSystemTimes`; ambos calculam `(totalDelta − idleDelta) / totalDelta`. O **disco**
    deixou de medir só a pasta `tcmine-data` e passou a mostrar o **uso total do drive** (removida a
    varredura recursiva e o cache de 30s). **RAM** já era global (inalterada). `SystemStatusCard`
    relabela "Dados"→"Disco" e a legenda da CPU "servidor"→"host".
  - Novo `ServerInstanceMetricsService` (Infrastructure/`ServerInstances`, **singleton**, sem BD):
    `SampleAsync` lê CPU/RAM ao vivo dos containers direto do daemon (`GetContainerStatsAsync`,
    `Stream=false`), indexando por Id parseado do nome `tcmine-mc-{guid}` (CPU pela fórmula do
    `docker stats`; memória = uso − page cache). `SampleDiskAsync` mede o **uso em disco** do
    diretório de cada instância (`servers/{id}`, rodando ou parada), varredura cacheada 30s em
    `Task.Run`. Record `ServerInstanceStats`.
  - Novo widget `ServerInstancesMetricsCard` (autocontido, Timer de 5s): **um card por instância**
    no dashboard — rodando → dois `MetricGauge` (CPU do container; RAM sobre o `-Xmx` configurado,
    já que o container não tem limite de cgroup); parada → card ocioso com o status. **Ambos**
    mostram o uso em disco no rodapé. Cruza a lista de `ServerInstanceService.ListAsync` com os
    stats via `InvokeAsync` (contexto do circuito, sem disputar o DbContext scoped).

- **[2026-07-05]** **Tela de configs dos jogadores + endurecimento da API de sync** (ver
  [[sources/2026-07-05-player-configs-admin-hardening]] e [[concepts/player-config-sync]]):
  - **Endurecimento** (`Endpoints/PlayerConfigEndpoints.cs`): `GET /manifest` e `POST /bundle`, antes
    **abertos**, agora exigem o token Minecraft do UUID (helper `AuthorizeReadAsync`); `PUT /push` ganhou
    **cota por conjunto** (`PlayerConfigs:MaxSetMb`, default 1 GB, `413` se exceder). O launcher passou a
    mandar `Authorization: Bearer` também nas leituras.
  - **Tela** `Admin/Players/` (`/admin/players`, Owner/Admin): `MudDataGrid` agrupado por jogador com
    tamanho/ficheiros/último sync e ações de apagar (conjunto ou tudo do jogador), sobre o novo
    `PlayerConfigAdminService` (Infrastructure, scoped). Nav link no `AdminLayout`.

## Relações

- Depende de [[entities/tcmine-server-infrastructure]], [[entities/tcmine-application]],
  [[entities/tcmine-design]]. Serve o [[entities/tcmine-launcher]].

## Pontos em aberto

- [x] CRUD admin de **modpacks** (2026-06-24).
- [x] CRUD de **usuários** (2026-06-25) — `/admin/users` (só Owner). Falta CRUD de releases.
- [x] Restringir `Settings` ao papel `Owner` (2026-06-25).
- [x] **Newsletter por modpack** (2026-06-24) — `NewsEntity.ModpackId` (FK
  opcional, null = global) + migration `NewsModpackFk` nos dois providers + aba
  Novidades. Ver [[concepts/modpack-admin-editor]].
- [x] **Feed global de novidades** (2026-06-25) — página `/admin/news` (globais + de
  modpacks) com seletor de modpack opcional no diálogo (vazio = global).
- [x] Painel de instâncias de servidor Minecraft (hub do modpack + tela do servidor com console/Monaco) —
      ver [[concepts/modpack-server-hub-ux]] e [[concepts/server-instance-lifecycle]].

## Referências

- Código: `TCMine-Server/Program.cs`, `Endpoints/`, `Authentication/`,
  `Components/`, `Theme/MudThemeFactory.cs`
- Fontes: [[sources/2026-06-23-leitura-codigo-vivo]], [[sources/2026-06-27-server-instances-e-ux]]
