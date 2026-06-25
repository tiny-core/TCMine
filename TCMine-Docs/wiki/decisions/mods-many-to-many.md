---
type: decision
title: Mods em N:N (ModFile + ModpackMod) em vez de FK 1:N
tags: [decision, ef-core, modpack, mods, persistência]
status: aceita
created: 2026-06-25
updated: 2026-06-25
deciders: [Jocian, Claude]
supersedes: []
superseded-by: []
sources:
  - "[[sources/2026-06-25-mods-many-to-many]]"
related:
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/modside-rules]]"
---

# Mods em N:N (ModFile + ModpackMod) em vez de FK 1:N

> O mesmo arquivo de mod usado em vários modpacks deixa de duplicar linhas: vira
> um `ModFile` compartilhado + uma junção `ModpackMod` por modpack.

## Contexto

A tabela `Mods` tinha uma FK `ModpackId` (1:N). Adicionar o mesmo mod a N modpacks
criava N linhas quase idênticas (Name, FileName, DownloadUrl, Sha1, FileLength…).
O **jar** já era deduplicado em disco (`tcmine-data/mods/{fileId}/...`, ver
[[concepts/modpack-mods-locais]]) — só os **metadados** duplicavam. Observação
levantada pelo usuário ao analisar o schema.

Detalhe decisivo: nem tudo em `Mods` é "do arquivo". `Side` e `Target` são
**escolhas por-modpack** (o mesmo mod pode ser `Client` num pack e `Both` noutro).

## Decisão

Normalizar em N:N:

- **`ModFileEntity`** — identidade pelo `FileId` (**PK natural**; id do CF, ou
  negativo derivado do conteúdo para uploads). Guarda os metadados intrínsecos uma
  só vez: CurseModId, Name, Version, FileName, DownloadUrl, Sha1, FileLength.
- **`ModpackModEntity`** — junção com **PK composta `(ModpackId, FileId)`** e os
  atributos por-modpack: `Side`, `Target`, `SortOrder`. FK p/ `Modpack` (cascade)
  e p/ `ModFile` (**restrict** — dropar de um pack não apaga o arquivo, que pode
  estar em outros).
- **`ModEntryEntity`** deixa de ser entidade EF e vira **modelo plano** (rascunho
  do editor + item de import). `ModpackImportService.SaveAsync` decompõe a lista
  plana em upsert de `ModFile` + reconciliação dos `ModpackMod`; `FlattenMods` faz
  o caminho inverso para o editor.

## Consequências

- **+** Metadados (hash/tamanho/URL) num só lugar; sem linhas duplicadas; modelo
  mais correto. Uploads do mesmo jar em packs diferentes convergem no mesmo
  `ModFile`.
- **+** `Side`/`Target` corretamente modelados como por-modpack na junção.
- **−** Refactor amplo (Domain, EF, manifesto, `ContentCatalog`, editor) +
  **migration destrutiva** nos dois providers (a tabela `Mods` é dropada; dados de
  mod existentes se perdem, mas os jars no cache permitem repovoar via re-save).
- **Órfãos:** `ModFile` sem nenhum vínculo não é apagado automaticamente (como já
  era a política do cache de jars) — eventual GC fica como trabalho futuro.
- **Dashboard:** KPI "Mods" passa a contar **arquivos únicos** (`ModFiles`); a
  distribuição cliente/servidor conta **vínculos** (`ModpackMods`).

## Alternativas consideradas

- **Manter 1:N:** duplicação é barata (jars já compartilhados); zero refactor.
  Rejeitada por ser o momento mais barato de normalizar (projeto cedo, pouco dado).
- **Surrogate Id + índice único em FileId:** preferimos `FileId` como PK natural
  (já é único/imutável e é a chave do cache de jars), simplificando a junção.

## Referências

- [[sources/2026-06-25-mods-many-to-many]]
- Código: `TCMine-Domain/Entities/{ModFileEntity,ModpackModEntity,ModEntryEntity}.cs`,
  `TCMine-Infrastructure/Persistence/AppDbContext.cs`,
  `TCMine-Infrastructure/Minecraft/ModpackImportService.cs`,
  `Migrations/{Sqlite,Postgres}/*_ModsManyToMany.cs`.
