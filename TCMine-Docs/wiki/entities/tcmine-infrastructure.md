---
type: entity
title: TCMine-Infrastructure
tags: [entity, tcmine, infrastructure, ef-core, curseforge]
status: wip
created: 2026-06-23
updated: 2026-06-25
aliases: [TCMine-Infrastructure, Infrastructure, Infra]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-server]]"
  - "[[decisions/persistence-dual-provider]]"
  - "[[decisions/mods-many-to-many]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/secrets-data-protection]]"
---

# TCMine-Infrastructure

> Implementações concretas das portas da [[entities/tcmine-application]]: EF Core
> (SQLite/Postgres), cliente CurseForge, filesystem, identidade e serviços de
> servidor/Minecraft.

## Visão geral

`TCMine-Infrastructure` (namespace `TCMine_Infrastructure`) é a camada externa
que toca o mundo (banco, rede, disco). Depende de Domain + Application; o servidor
a registra no DI.

## Responsabilidades / Escopo

- **Persistence (`Persistence/`):** banco **dual-provider** — `AppDbContext`
  **abstrato**, com `SqliteAppDbContext`/`PostgresAppDbContext` concretos e
  **migrations próprias por provider**. `AddTcMineDatabase` escolhe o provider
  (env `DB_PROVIDER`/`DB_CONNECTION` > seção `Database` > default SQLite) e mapeia
  a base abstrata → concreta; `MigrateTcMineDatabaseAsync` aplica migrations no
  boot. SQLite default: `Data Source=data-server/tcmine.db`. Repositórios:
  `UserRepository`, `PlayerConfigRepository`, `ServerSettingsStore`. Ver
  [[decisions/persistence-dual-provider]].
  - `OnModelCreating` concentra: `Username` único; chave composta de
    `PlayerConfigEntity` `(Uuid, ModpackId)`; `ServerSettingEntity` linha única
    (`Id == 1`, `ValueGeneratedNever`); cascatas modpack→mods/servers; enums como
    texto; `ServerInstance` com `Restrict` no FK do modpack. **Mods em N:N**
    (migrations `ModsManyToMany` + `ModFileOrphanMarker`): `ModFileEntity` (PK
    `FileId`, com `OrphanedAt` para marcar órfãos) + junção `ModpackModEntity`
    `(ModpackId, FileId)` — cascade no modpack, `Restrict` no arquivo. O
    `ModpackImportService` mantém o marcador (`MarkOrphansAsync`) e faz GC de órfãos
    (`DeleteOrphanFileAsync`). Ver [[decisions/mods-many-to-many]].
- **CurseForge (`CurseForge/`):** `CurseForgeApiClient` implementa `ICurseForgeApi`,
  falando direto com `api.curseforge.com` e injetando a `x-api-key` (lida das
  settings cifradas) **por requisição**. Busca de mods/modpacks (game 432),
  resolução de arquivos em lote. Ver [[concepts/curseforge-proxy]].
- **FileSystem (`FileSystem/`):** `ServerPaths` centraliza `tcmine-data/`
  (`updates`, `secrets`, `servers`, `modpacks`, `mods`), criados no boot.
- **Identity (`Identity/`):** `UserService` (login, hash de senha),
  `SetupState` (detecção de primeira execução) — ver [[concepts/setup-auth-cookie]].
- **Minecraft (`Minecraft/`):** `MinecraftAuthService` (valida token Minecraft,
  cacheia), `MinecraftVersionService` (versões oficiais MC+loaders),
  `ModpackImportService` (baixa jars, infere Side, persiste).
- **Server (`Server/`):** `ContentCatalog` (catálogo em memória), `ContentNotifier`
  (SSE de sync — ver [[concepts/sse-content-sync]]), `SystemMetricsService`,
  `ServerSettingsService` (settings de runtime cifradas — ver
  [[concepts/secrets-data-protection]]).
- **Launcher (`Launcher/`):** `LauncherFeedService` (inspeciona `tcmine-data/
  updates` para o feed Velopack).

## Decisões e estado atual

- **[2026-06-23]** `ServerSettingsService` é **singleton com cache** (leitura
  quente: o proxy CF consulta a cada request) e abre escopo curto via
  `IServiceScopeFactory` para tocar o `AppDbContext` (que é scoped). Segredos
  cifrados com Data Protection (`protector "TCMine.ServerSettings.v1"`); evento
  `Changed` notifica a UI. **Sem fallback para env vars** — settings vêm só do banco.

## Relações

- Implementa as portas de [[entities/tcmine-application]]; registrada por
  [[entities/tcmine-server]].

## Pontos em aberto

- [ ] Detalhar `ModpackImportService`, `MinecraftAuthService` e a orquestração de
      `ServerInstance` (ainda modelada, não operada).

## Referências

- Código: `TCMine-Infrastructure/Persistence/`, `CurseForge/`, `FileSystem/`,
  `Identity/`, `Minecraft/`, `Server/`, `Launcher/`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
