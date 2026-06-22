---
type: entity
title: TCMine-Server
tags: [entity, tcmine, blazor, minimal-api, backend, admin]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine-Server, servidor, backend, painel admin]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-design]]"
  - "[[concepts/setup-auth-cookie]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/player-config-sync]]"
---

# TCMine-Server

> ASP.NET Core **.NET 10**: Minimal API (backend que o launcher consome) +
> Blazor Server (painel admin). É o ponto central do ecossistema.

## Visão geral

`TCMine-Server` (namespace `TCMine_Server`) é o backend e o painel de gestão.
Serve o **catálogo/manifesto de modpacks** e os **jars** ao launcher, faz **proxy
do CurseForge**, **sync de conteúdo via SSE**, **sync de configs do jogador**, e
oferece a UI admin (Blazor Server + MudBlazor) para gerenciar tudo.

## Responsabilidades / Escopo

- **Bootstrap (`Program.cs`)**: Razor Components interativos + MudBlazor; Data
  Protection (chaves em `tcmine-data/secrets`); EF Core via `AddTcMineDatabase`;
  migrations no boot; HttpClient nomeado do CurseForge; auth por cookie
  (`tcmine_auth`, 7 dias, sliding); rate limiter por IP (política `configs`);
  middleware de **primeira execução** (redireciona a `/setup`) e de **404
  estilizado** (buffer da resposta de página + promove status a 404).
- **Endpoints** (Minimal API, consumidos pelo launcher):
  - `/api/modpacks` (catálogo, só publicados) e `/api/modpacks/{uid}` (manifesto
    detalhado, **só mods do lado cliente**, URLs reescritas para o servidor).
  - `/files/{fileId}/{fileName}` — serving dos jars do cache (ver
    [[concepts/modpack-mods-locais]]).
  - `/api/modpacks/{uid}/overrides.zip` — bundle de overrides sob demanda.
  - `/v1/{**path}` — **proxy CurseForge** ([[concepts/curseforge-proxy]]).
  - `/events` — SSE de sync de conteúdo.
  - `/players/{uuid}/configs/{modpackId}` — PUT de configs do jogador
    ([[concepts/player-config-sync]]).
  - `/download` — Setup.exe mais recente do launcher; `/updates` — feed Velopack (estático).
  - `/auth/logout`.
- **Authentication**: `AuthClaims` (monta o `ClaimsPrincipal`),
  `PersistingAuthenticationStateProvider` (identidade do cookie persistida do
  prerender para o circuito).
- **Components** (Blazor): layouts (`RootLayout`/`AdminLayout`/`PublicLayout`/
  `MainLayout`), páginas (`Home`, `Login`, `Setup`, `Error`, `NotFound`,
  `Admin/Dashboard`), widgets do dashboard (`DashboardKpis`, `SystemStatusCard`,
  `RecentActivityCard`, `RecentModpacksCard`, `ModDistributionCard`,
  `DashboardHeader`), shared (`StatCard`, `CenterScreen`, `ErrorScreen`,
  `RelativeTime`).
- **Theme**: `MudThemeFactory.Create()` constrói o `MudTheme` (PaletteDark +
  PaletteLight) a partir de [[entities/tcmine-design]].

## Decisões e estado atual

- **[2026-06-22]** Auth por **cookie** + `PersistingAuthenticationStateProvider`
  (substituiu estado em memória perdido no F5); login/logout fazem reload SSR
  completo. Ver [[concepts/setup-auth-cookie]].
- **[2026-06-22]** Manifesto público é **filtrado para o lado cliente** e reescreve
  URLs dos mods para `/files/...` — launcher nunca baixa do CurseForge.
- **[2026-06-22]** **404 estilizado**: a `NotFound.razor` (catch-all) renderiza o
  corpo a 200 e marca o `HttpContext`; um middleware promove a 404 (rotas de API/
  SSE/assets ficam de fora).
- **[2026-06-22]** `BlazorDisableThrowNavigationException=true`; Docker target Linux.

## Relações

- Depende de [[entities/tcmine-infrastructure]], [[entities/tcmine-application]], [[entities/tcmine-design]].
- Serve o [[entities/tcmine-launcher]].

## Pontos em aberto

- [ ] Páginas admin de CRUD de modpacks/usuários/releases além do Dashboard.
- [ ] Orquestração de instâncias de servidor Minecraft.

## Referências

- Código: `TCMine-Server/Program.cs`, `Endpoints/`, `Authentication/`, `Components/`, `Theme/`
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
