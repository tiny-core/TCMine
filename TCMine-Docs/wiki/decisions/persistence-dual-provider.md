---
type: decision
title: Banco dual-provider (SQLite + Postgres)
tags: [decision, ef-core, persistência, arquitetura]
status: aceita
created: 2026-06-23
updated: 2026-06-23
deciders: [Jocian]
supersedes: []
superseded-by: []
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[concepts/clean-architecture]]"
---

# Banco dual-provider (SQLite + Postgres)

> Suportar SQLite **e** Postgres com um `AppDbContext` **abstrato** + subclasses
> concretas por provider, cada uma com o seu conjunto de migrations.

## Contexto

O TCMine deve rodar fácil em dev/instalações pequenas (SQLite, zero setup) e
também em produção (Postgres). O EF Core mantém **um snapshot de modelo por tipo
de contexto** e as migrations de SQLite e Postgres **não são intercambiáveis**
(tipos de coluna divergem). Não dá para um único `DbContext` com um único conjunto
de migrations servir os dois.

## Decisão

- `AppDbContext` é **abstrato**; `SqliteAppDbContext` e `PostgresAppDbContext` são
  concretos, cada um com o **seu** conjunto de migrations
  (`Migrations/Sqlite/`, `Migrations/Postgres/`).
- `AddTcMineDatabase` escolhe o provider por configuração — prioridade: env
  `DB_PROVIDER`/`DB_CONNECTION` > seção `Database` do `appsettings` > **default
  SQLite** (`Data Source=data-server/tcmine.db`) — e mapeia a base abstrata para a
  concreta no DI (`AddScoped<AppDbContext>(sp => sp.Get...Concrete())`).
- Os **serviços dependem só de `AppDbContext`** (a base) e ignoram o provider.
- `MigrateTcMineDatabaseAsync` aplica as migrations no boot, resolvendo a
  subclasse já escolhida pelo DI.

## Consequências

- **+** Serviços e repositórios são agnósticos de provider.
- **+** Troca de provider é configuração, não código.
- **−** **Dois** conjuntos de migrations para manter: ao mudar o modelo, gerar a
  migration para **cada** contexto (`--context SqliteAppDbContext` e
  `--context PostgresAppDbContext`).

## Alternativas consideradas

- **Provider único** — mais simples, mas perde o dev-friendly (SQLite) ou o
  prod-grade (Postgres).
- **Trocar provider em runtime no mesmo contexto** — inviável: snapshots/migrations
  não são compatíveis entre providers.

## Referências

- `TCMine-Server.Infrastructure/Persistence/{AppDbContext,DatabaseServiceCollectionExtensions,SqliteAppDbContext,PostgresAppDbContext}.cs`
- [[entities/tcmine-server-infrastructure]] · [[sources/2026-06-23-leitura-codigo-vivo]]
