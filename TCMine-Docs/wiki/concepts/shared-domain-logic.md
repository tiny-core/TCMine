---
type: concept
title: Lógica de domínio compartilhada no core
tags: [concept, arquitetura, modpack, domain]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [shared domain logic, lógica compartilhada, core compartilhado]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[concepts/clean-architecture]]"
  - "[[concepts/modside-rules]]"
  - "[[concepts/curseforge-proxy]]"
---

# Lógica de domínio compartilhada no core

> O que servidor e launcher precisam decidir **igual** vive no core (Domain/
> Application), não duplicado nos dois lados.

## O que é

Um princípio do projeto: regras que tanto o [[entities/tcmine-server]] quanto o
[[entities/tcmine-launcher]] aplicam devem ter **uma** implementação, no core
compartilhado, em vez de cópias que podem divergir.

## Por que importa para o TCMine

Se a filtragem por lado, o parse de loader ou o merge de mods fossem
reimplementados em cada app, bastaria uma correção num lado para os dois saírem
de sincronia. Como cliente e servidor têm de chegar à **mesma** decisão sobre o
mesmo modpack, divergência = bug silencioso.

## Detalhes / Variações

Lógica **pura** (estática, sem I/O direto) que mora no core:

- **`ModSideRules`** (Domain) — filtragem por lado; ver [[concepts/modside-rules]].
- **`ModLoaders.ParseId`** (Domain) — interpreta `"neoforge-21.1.77"` →
  (loader, versão); prefixo desconhecido → NeoForge.
- **`ModSetMerge.Merge`** (Application) — mescla listas de mods por chave (id do
  mod), preservando ordem (atual + novos), reportando `Added`/`Updated`.
- **`CurseForgeImporter`** (Application, abstrato) — lê `manifest.json`, resolve
  arquivos/mods e monta `ImportedModpackDto`. O **acesso** ao CurseForge é
  injetado (`ICurseForgeApi`): o servidor passa a API direta (key), o cliente
  passaria o proxy (ver [[concepts/curseforge-proxy]]). `InferSide` deduz o lado
  a partir do *server pack*.

## Aplicação concreta

- `TCMine-Domain/Modpack/{ModSideRules,ModLoaders}.cs`;
  `TCMine-Application/Modpack/{ModSetMerge,CurseForgeImporter}.cs`.

## Contradições / debates conhecidos

- O launcher ainda é scaffolded; na prática hoje só o servidor exercita esse
  core. A simetria é a intenção de projeto, validada quando o launcher consumir a
  mesma lógica.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
