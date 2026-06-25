---
type: entity
title: TCMine-Domain
tags: [entity, tcmine, domain, clean-architecture]
status: wip
created: 2026-06-23
updated: 2026-06-25
aliases: [TCMine-Domain, domínio, Domain]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[concepts/clean-architecture]]"
  - "[[decisions/mods-many-to-many]]"
---

# TCMine-Domain

> Camada mais interna da Clean Architecture: entidades, enums e regras **puras**
> de domínio. Sem dependência de EF Core ou ASP.NET.

## Visão geral

`TCMine-Domain` (namespace `TCMine_Domain`) guarda o modelo de domínio do TCMine
e as regras que não dependem de infraestrutura. É referenciado por todas as
outras camadas e não referencia nenhuma (a não ser `System.ComponentModel.
DataAnnotations` para anotações como `[MaxLength]`).

## Responsabilidades / Escopo

- **Entidades (`Entities/`):** `NewsEntity`, `ModpackEntity`, `ModFileEntity`,
  `ModpackModEntity`, `ServerEntryEntity`, `ReleaseEntity`, `PlayerConfigEntity`,
  `ServerSettingEntity`, `UserEntity`, `ServerInstanceEntity`, `OverrideHistoryEntry`.
  São POCOs; o mapeamento EF (chaves, conversões, cascatas) é feito em
  [[entities/tcmine-infrastructure]] (`AppDbContext.OnModelCreating`), não aqui.
  - `ModpackEntity`: `Id` é `Guid`; tem `Name`/`Version`/`Minecraft`/`Loader`/
    `LoaderVersion`/`Description`/`IsPublished`/`RecommendedRamMb`/`HasOverrides`/
    `UpdatedAt`, e listas `Mods` (vínculos `ModpackModEntity`) + `Servers`.
  - **Mods em N:N** (ver [[decisions/mods-many-to-many]]): `ModFileEntity` (PK
    `FileId`, metadados do arquivo uma vez) + `ModpackModEntity` (junção
    `(ModpackId, FileId)` com `Side`/`Target`/`SortOrder`). `ModEntryEntity` **não**
    é entidade EF — é o modelo plano de rascunho/import do editor.
- **Modpack (`Modpack/`):**
  - `ModSide` (`Both`/`Client`/`Server`, default `Both`) + `ModSideRules`
    (`RunsOnClient`/`RunsOnServer`) — a fonte única de filtragem por lado, ver
    [[concepts/modside-rules]].
  - `ModLoader` (`NeoForge`/`Forge`/`Fabric`/`Quilt`) + `ModLoaders.ParseId`
    (interpreta `"neoforge-21.1.77"` → tipo+versão; prefixo desconhecido →
    NeoForge) e `DisplayName`.
- **Identity (`Identity/`):** `UserRole` (`Owner` > `Admin` > `Operator` >
  `Viewer`), guardado como texto no banco — ver [[concepts/setup-auth-cookie]].

## Decisões e estado atual

- **[2026-06-23]** Enums críticos (`ModSide`, `ModLoader`, `UserRole`) são
  persistidos como **string** (conversão em `AppDbContext`) — legível no banco e
  estável a reordenações do enum.
- **[2026-06-23]** O projeto **não fica preso ao NeoForge**: o loader é uma
  dimensão de primeira classe do domínio.

## Relações

- Base de [[entities/tcmine-application]] e [[entities/tcmine-infrastructure]].
- Concretiza [[concepts/clean-architecture]] (camada interna).

## Pontos em aberto

- [ ] Documentar as demais entidades (`ServerInstanceEntity`, `ReleaseEntity`,
      `OverrideHistoryEntry`) quando forem aprofundadas.

## Referências

- Código: `TCMine-Domain/Entities/`, `TCMine-Domain/Modpack/`, `TCMine-Domain/Identity/`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
