---
type: concept
title: Mods servidos pelo próprio servidor
tags: [concept, modpack, mods, download, curseforge]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [project-modpack-mods-locais, mods locais, serving de jars, cache de mods]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-domain]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/modside-rules]]"
---

# Mods servidos pelo próprio servidor

> O launcher baixa os jars **do servidor TCMine** (`/files/{fileId}/{fileName}`),
> nunca direto do CurseForge. O servidor baixa do CF uma vez e mantém um cache.

> Nota: o código referencia este conceito como `project-modpack-mods-locais`
> (ver comentários em `ModEntryEntity` e `ModpackEndpoints`).

## O que é

Princípio de distribuição: os arquivos `.jar` dos mods são **hospedados pelo
servidor**. Ao gerar o manifesto detalhado, o servidor **reescreve a URL** de cada
mod para `{baseUrl}/files/{fileId}/{fileName}`. O launcher baixa daqui.

## Por que importa para o TCMine

- **Confiabilidade/velocidade**: o cache de jars fica sob `tcmine-data/mods/{fileId}/`
  (compartilhado por todos os modpacks que usam o mesmo arquivo — dedup por `fileId`).
- **Integridade**: `ModEntryEntity` guarda `Sha1`/`FileLength` (preenchidos quando
  o servidor baixa), para o launcher verificar.
- **Independência do CF no cliente**: o `DownloadUrl` de origem (CurseForge) **só o
  servidor usa**; o cliente não toca o CF para baixar (ver também
  [[concepts/curseforge-proxy]] para a parte de busca/resolução).

## Detalhes / Variações

- `GET /files/{fileId}/{fileName}` serve do cache; `Path.GetFileName` neutraliza
  path traversal; content-type `application/java-archive`.
- `GET /api/modpacks/{uid}` reescreve URLs e filtra por `RunsOnClient`
  ([[concepts/modside-rules]]).
- `GET /api/modpacks/{uid}/overrides.zip` empacota a pasta `overrides` editável em disco.
- A `baseUrl` vem de `PublicBaseUrl` (canônico atrás de proxy) ou da requisição.

## Aplicação concreta

- `TCMine-Server/Endpoints/ModpackEndpoints.cs`; cache em
  `ServerPaths.Mods(...)`; `ModEntryEntity` em [[entities/tcmine-domain]].

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
