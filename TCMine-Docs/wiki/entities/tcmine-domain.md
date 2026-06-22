---
type: entity
title: TCMine-Domain
tags: [entity, tcmine, domain, clean-architecture]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine-Domain, camada de domínio]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[concepts/clean-architecture]]"
  - "[[concepts/modside-rules]]"
---

# TCMine-Domain

> Camada mais interna da Clean Architecture: entidades e regras de domínio,
> sem dependências de framework.

## Visão geral

`TCMine-Domain` (namespace `TCMine_Domain`) guarda as **entidades persistidas**,
os **enums de domínio** e as **regras puras** que servidor e launcher
compartilham. Não referencia EF Core nem ASP.NET — é o núcleo estável.

## Responsabilidades / Escopo

- **Entities** (`Entities/`):
  - `ModpackEntity` — modpack oficial (slug = `Id` Guid): nome, versão, `Minecraft`,
    `ModLoader` + `LoaderVersion`, descrição, `IsPublished`, `RecommendedRamMb`,
    `HasOverrides`, `UpdatedAt`; coleções `Mods` e `Servers`.
  - `ModEntryEntity` — um mod CurseForge de um modpack: `CurseModId`/`FileId`,
    `Name`/`Version`/`FileName`, `DownloadUrl` (origem CF, só o servidor usa),
    `Sha1`/`FileLength` (integridade), `Target` (mod/resourcepack/shaderpack) e
    `Side` (`ModSide`).
  - `ServerEntryEntity` — servidor anunciado por um modpack (vai para `servers.dat`).
  - `ServerInstanceEntity` — instância de servidor Minecraft gerenciada (status,
    Pid, RAM, porta…). **Hoje só persistência**; orquestração virá depois.
  - `UserEntity` — usuário do painel (login, `PasswordHash` PBKDF2, `UserRole`,
    `IsActive`, `LastLoginAt`).
  - `ServerSettingEntity` — linha única (Id==1) de settings de runtime (CF token
    cifrado, Azure client/tenant id, `PublicBaseUrl`).
  - `PlayerConfigEntity` — configs do jogador por `(Uuid, ModpackId)`, last-write-wins.
  - `NewsEntity`, `ReleaseEntity`, `OverrideHistoryEntry` (trilha de auditoria/undo dos overrides).
- **Identity** (`Identity/UserRole.cs`): enum `UserRole` — `Owner` > `Admin` >
  `Operator` > `Viewer`.
- **Modpack** (`Modpack/`):
  - `ModLoader` (`NeoForge`/`Forge`/`Fabric`/`Quilt`) + helpers `ModLoaders`
    (`ParseId`, `DisplayName`).
  - `ModSide` (`Both`/`Client`/`Server`) + `ModSideRules` (`RunsOnClient`/`RunsOnServer`)
    — ver [[concepts/modside-rules]].

## Decisões e estado atual

- **[2026-06-22]** `UserRole` e `ModLoader` guardados como **string** no banco
  (mapeamento no `AppDbContext`): legível e estável a reordenações do enum.
- **[2026-06-22]** O projeto **não fica preso ao NeoForge** — `ModLoader` é
  multi-loader desde o domínio.
- **[2026-06-22]** `ModSide` é dimensão de domínio **compartilhada** (não duplicada
  nos dois lados), distinta de `Target` (pasta destino no cliente).

## Relações

- É a base de [[entities/tcmine-application]] e [[entities/tcmine-infrastructure]].
- Implementa [[concepts/clean-architecture]] (camada interna) e [[concepts/modside-rules]].

## Pontos em aberto

- [ ] Orquestração de `ServerInstanceEntity` (campos de runtime hoje só refletem último estado conhecido).

## Referências

- Código: `raw/code-refs/2026-06-22-leitura-inicial-solucao.md`
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
