---
type: entity
title: TCMine-Application
tags: [entity, tcmine, application, clean-architecture, modpack]
status: wip
created: 2026-06-23
updated: 2026-06-23
aliases: [TCMine-Application, Application, casos de uso]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[concepts/shared-domain-logic]]"
  - "[[concepts/dtos-as-records]]"
---

# TCMine-Application

> Camada de aplicação: **portas** (interfaces), **contratos** (DTOs `record`) e
> **lógica pura de modpack** compartilhada por servidor e launcher. Depende só de
> [[entities/tcmine-domain]].

## Visão geral

`TCMine-Application` (namespace `TCMine_Application`) define *o que* a aplicação
faz sem dizer *como* a infraestrutura o realiza. As implementações concretas
ficam em [[entities/tcmine-server-infrastructure]]; servidor e launcher injetam a sua
própria versão das portas.

## Responsabilidades / Escopo

- **Portas (`Abstractions/`):** `ICurseForgeApi` (o servidor implementa com a API
  direta + key; o cliente, sobre o proxy — ver [[concepts/curseforge-proxy]]),
  `IUserRepository`, `IPlayerConfigRepository`, `IServerSettingsStore`.
- **Contratos (`Contracts/`):** DTOs imutáveis como `record` — `ModDto`,
  `ModpackManifestDto`, `ImportedModpackDto`/`ImportedModDto`, `MergeResultDto<T>`,
  `ModpackAdminRowDto`, `SaveProgressDto`, `OverrideFileDto`, `DraftImportDto<T>`,
  `VersionOptionDto`, além dos contratos de CurseForge e Server. Ver
  [[concepts/dtos-as-records]].
- **Lógica de modpack (`Modpack/`):**
  - `CurseForgeImporter` (abstrato, **puro**): lê `manifest.json` do `.zip`,
    resolve arquivos/mods via `ICurseForgeApi`, monta `ImportedModpackDto` +
    bundle de overrides. `InferSide` deduz o `ModSide` a partir do *server pack*
    (mod presente no pack ⇒ `Both`; ausente ⇒ `Client`). `ClassToTarget` mapeia
    classe CF → pasta (`mod`/`resourcepack`/`shaderpack`).
  - `ModSetMerge.Merge`: mescla (não substitui) listas de mods por chave
    (id do mod), preservando ordem — ver [[concepts/shared-domain-logic]].
- **Identity (`Identity/`):** `UserInfo`.

## Decisões e estado atual

- **[2026-06-23]** A lógica de import/merge é **estática e pura** (sem I/O
  direto), recebendo o acesso ao CurseForge por injeção — por isso roda igual no
  servidor (key) e no cliente (proxy).
- **[2026-06-23]** Os DTOs de fio são `record` imutáveis; ver
  [[concepts/dtos-as-records]].

## Relações

- Implementada por [[entities/tcmine-server-infrastructure]]; consumida por
  [[entities/tcmine-server]] e [[entities/tcmine-launcher]].

## Pontos em aberto

- [ ] Documentar `Contracts/Server.cs` e `Contracts/CurseForge.cs` em detalhe.

## Referências

- Código: `TCMine-Application/Abstractions/`, `Contracts/`, `Modpack/`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
