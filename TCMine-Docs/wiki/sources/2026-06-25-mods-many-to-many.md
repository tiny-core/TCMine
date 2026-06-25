---
type: source
title: Normalização de mods em N:N (ModFile + ModpackMod)
tags: [source, code, ef-core, modpack]
status: ingested
created: 2026-06-25
updated: 2026-06-25
source-type: code
origin: "Pergunta do usuário sobre o schema + refactor no código vivo (Domain/Infra/Server)"
feeds:
  - "[[decisions/mods-many-to-many]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-infrastructure]]"
related: []
---

# Normalização de mods em N:N (ModFile + ModpackMod)

> O usuário notou que a FK 1:N duplicava linhas do mesmo mod entre modpacks e
> propôs uma tabela de junção. Implementado.

## Resumo

`Mods` (FK `ModpackId` 1:N) → `ModFileEntity` (PK `FileId`, metadados do arquivo
uma vez) + `ModpackModEntity` (junção PK composta `(ModpackId, FileId)` com
`Side`/`Target`/`SortOrder`). `ModEntryEntity` virou modelo plano (não-EF) usado
pelo editor e import; `SaveAsync` decompõe em upsert de `ModFile` + reconcile dos
vínculos; `FlattenMods` reidrata para o editor. Migrations destrutivas geradas nos
dois providers.

## Pontos-chave

- O jar já era compartilhado em disco; só metadados duplicavam.
- `Side`/`Target` são por-modpack → vivem na junção.
- Manifesto, `ContentCatalog` (counts) e o editor Blazor foram ajustados.
- Migration destrutiva: a tabela `Mods` é dropada (dados de mod perdidos; jars no
  cache permitem repovoar via re-save).

## O que alimentou na wiki

- Criou [[decisions/mods-many-to-many]].
- Atualizou [[concepts/modpack-mods-locais]], [[entities/tcmine-domain]],
  [[entities/tcmine-infrastructure]].

## Referências

- `TCMine-Domain/Entities/{ModFileEntity,ModpackModEntity,ModEntryEntity}.cs`.
- `TCMine-Infrastructure/Minecraft/ModpackImportService.cs`.
