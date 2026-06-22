---
type: entity
title: TCMine-Application
tags: [entity, tcmine, application, clean-architecture]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine-Application, camada de aplicação]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-domain]]"
  - "[[concepts/clean-architecture]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/curseforge-proxy]]"
---

# TCMine-Application

> Camada de aplicação: **portas** (interfaces), **contratos** (DTOs) e **lógica
> pura** de modpack compartilhada por servidor e launcher.

## Visão geral

`TCMine-Application` (namespace `TCMine_Application`) define o que a aplicação
precisa do mundo externo (via interfaces implementadas na Infrastructure) e
concentra a lógica **neutra de framework** — sobretudo a montagem/mesclagem de
modpacks que os dois lados (servidor e launcher) usam idêntica.

## Responsabilidades / Escopo

- **Abstractions** (portas): `ICurseForgeApi` (operações que o importador precisa
  do CurseForge — o servidor implementa com a key, o cliente sobre o proxy),
  `IUserRepository`, `IPlayerConfigRepository`, `IServerSettingsStore`.
- **Contracts** (DTOs):
  - CurseForge: `CfManifestDto` e parentes, `CfFileRefDto`, `CfModRefDto`,
    `CfSearchResultDto`.
  - Modpack: `ModDto`, `ModpackManifestDto`, `ImportedModpackDto`/`ImportedModDto`,
    `ModpackAdminRowDto`, `DraftImportDto<T>`, `MergeResultDto<T>`, `SaveProgressDto`,
    `OverrideFileDto`, `VersionOptionDto`.
  - Server: `NewsDto`, `ModpackSummaryDto`, `ServerDto`, `ReleaseDto`.
- **Identity**: `UserInfo` — forma serializável da identidade persistida entre
  prerender e circuito Blazor (`FromPrincipal`/`ToPrincipal`).
- **Modpack** (lógica pura, `static`/`abstract`):
  - `CurseForgeImporter` — lê `manifest.json` do zip, resolve arquivos/mods via
    `ICurseForgeApi`, devolve `ImportedModpackDto` + overrides. Inclui
    `ImportSingleAsync`, `InferSide` (lado a partir do server pack),
    `ClassToTarget` (classId CF → mod/resourcepack/shaderpack), `ResolveDownloadUrl`.
  - `ModSetMerge` — mescla listas de mods por chave (id CF): novos adicionados,
    existentes atualizados, ausentes mantidos; preserva ordem.

## Decisões e estado atual

- **[2026-06-22]** Acesso ao CurseForge é uma **porta** (`ICurseForgeApi`): servidor
  usa API direta (key + POST em lote), launcher usa o proxy — ver
  [[concepts/curseforge-proxy]].
- **[2026-06-22]** `CurseForgeImporter` é **lógica pura compartilhada** (abstrata,
  sem estado): a mesma montagem roda no admin e no launcher.
- **[2026-06-22]** `InferSide`: mod presente no server pack ⇒ `Both`; ausente ⇒
  `Client`; sem server pack ⇒ `Both` (admin ajusta). Ver [[concepts/modside-rules]].

## Relações

- Depende de [[entities/tcmine-domain]].
- Implementada por [[entities/tcmine-infrastructure]] (repositórios, CF client).
- Consumida por [[entities/tcmine-server]] (e futuramente pelo [[entities/tcmine-launcher]]).

## Pontos em aberto

- [ ] Consumo da camada pelo launcher (hoje só o servidor a usa de fato).

## Referências

- Código: `raw/code-refs/2026-06-22-leitura-inicial-solucao.md`
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
