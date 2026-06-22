---
type: concept
title: Persistência com dois providers (SQLite/Postgres)
tags: [concept, ef-core, persistencia, sqlite, postgres]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [dual provider, SQLite Postgres, AppDbContext abstrato, migrations]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-domain]]"
---

# Persistência com dois providers (SQLite/Postgres)

> O banco suporta **SQLite** (default, zero-config) e **Postgres**, com um
> `AppDbContext` abstrato e uma subclasse concreta + migrations por provider.

## O que é

`AppDbContext` é **abstrato** porque o EF Core mantém um snapshot de modelo por
tipo de contexto e migrations de SQLite e Postgres não são intercambiáveis (tipos
de coluna diferem). Cada provider tem a sua subclasse concreta
(`SqliteAppDbContext` / `PostgresAppDbContext`) com o seu conjunto de migrations.
Os serviços dependem só da base — o DI resolve a concreta.

## Por que importa para o TCMine

- **SQLite** para dev local e deploys simples (sem dependência externa);
  **Postgres** para produção.
- Config de **bootstrap** (provider + connection string) fica **fora do banco** —
  é ela que diz como conectar. Demais settings vivem no banco.

## Detalhes / Variações

- `AddTcMineDatabase(config)`: prioridade **env vars `DB_PROVIDER`/`DB_CONNECTION`
  > seção `Database` do appsettings > padrão (SQLite)**. Env vars ganham para
  facilitar Docker/produção.
- Defaults: SQLite `Data Source=data-server/tcmine.db`; Postgres
  `Host=localhost;Database=tcmine;...`.
- `MigrateTcMineDatabaseAsync()` aplica migrations pendentes no boot.
- Mapeamentos no `OnModelCreating`: `UserEntity.Username` único; enums (`UserRole`,
  `ModLoader`) guardados como **string**.

## Aplicação concreta

- `TCMine-Infrastructure/Persistence/` (`AppDbContext`, `DatabaseOptions`,
  `DatabaseServiceCollectionExtensions`, subclasses, `Migrations/Sqlite` e
  `Migrations/Postgres`); ver [[entities/tcmine-infrastructure]].

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
