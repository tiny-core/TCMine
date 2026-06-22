---
type: entity
title: TCMine-Infrastructure
tags: [entity, tcmine, infrastructure, ef-core, clean-architecture]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine-Infrastructure, camada de infraestrutura]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-application]]"
  - "[[concepts/persistence-dual-provider]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/setup-auth-cookie]]"
  - "[[concepts/player-config-sync]]"
---

# TCMine-Infrastructure

> Implementações concretas das portas da Application: EF Core (SQLite/Postgres),
> CurseForge, filesystem, identidade, serviços de servidor e Minecraft.

## Visão geral

`TCMine-Infrastructure` (namespace `TCMine_Infrastructure`) liga a aplicação ao
mundo real: banco de dados, sistema de arquivos, APIs externas (CurseForge,
Mojang) e serviços com estado. Implementa as interfaces de
[[entities/tcmine-application]].

## Responsabilidades / Escopo

- **Persistence** — `AppDbContext` **abstrato** (DbSets de todas as entidades,
  `OnModelCreating`) com subclasses concretas `SqliteAppDbContext` /
  `PostgresAppDbContext`, cada uma com suas migrations; `DatabaseOptions`,
  `DatabaseServiceCollectionExtensions` (`AddTcMineDatabase`/`MigrateTcMineDatabaseAsync`),
  `DesignTimeDbContextFactories`; repositórios (`UserRepository`,
  `PlayerConfigRepository`, `ServerSettingsStore`). Ver
  [[concepts/persistence-dual-provider]].
- **FileSystem** — `ServerPaths`: centraliza os diretórios sob `tcmine-data/`
  (`updates`, `secrets`, `servers`, `modpacks`, `mods`) e garante que existem.
- **Identity** — `UserService` (hash de senha, normalização do login),
  `SetupState` (detecção de primeira execução, singleton com cache).
- **Server** — `ServerSettingsService` (settings de runtime, secrets cifrados via
  Data Protection, cache), `ContentCatalog`, `ContentNotifier` (SSE),
  `SystemMetricsService`.
- **CurseForge** — `CurseForgeApiClient` (implementa `ICurseForgeApi` com a key direta).
- **Launcher** — `LauncherFeedService` (inspeciona `tcmine-data/updates` para o feed Velopack).
- **Minecraft** — `MinecraftAuthService` (valida token Mojang, fail-open),
  `MinecraftVersionService` (versões oficiais para os seletores),
  `ModpackImportService` (baixa jars, infere `Side`, persiste).

## Decisões e estado atual

- **[2026-06-22]** `AppDbContext` **abstrato** porque o EF Core tem um snapshot de
  modelo por tipo de contexto e migrations SQLite≠Postgres não são intercambiáveis;
  os serviços dependem só da base, o DI resolve a concreta.
- **[2026-06-22]** Config de **bootstrap** do banco (provider/connection) fica
  fora do banco; demais settings (CF token, Azure) vivem no banco cifradas.
- **[2026-06-22]** `MinecraftAuthService` é **fail-open**: se a Mojang cair,
  autoriza (são settings de jogo, sem segredos); só nega com 401/403 ou UUID
  divergente. Cache ~10 min.
- **[2026-06-22]** `ServerSettingsService` é singleton com cache; como o context é
  scoped, abre escopo curto via `IServiceScopeFactory`.

## Relações

- Implementa as portas de [[entities/tcmine-application]] sobre [[entities/tcmine-domain]].
- Consumida por [[entities/tcmine-server]] (registro no `Program.cs`).
- Materializa [[concepts/persistence-dual-provider]], [[concepts/player-config-sync]],
  [[concepts/setup-auth-cookie]].

## Pontos em aberto

- [ ] Orquestração real de instâncias de servidor (processo Java) ainda não implementada.

## Referências

- Código: `raw/code-refs/2026-06-22-leitura-inicial-solucao.md`
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
