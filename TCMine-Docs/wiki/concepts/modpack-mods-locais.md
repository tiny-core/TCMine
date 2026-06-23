---
type: concept
title: Mods servidos pelo próprio servidor
tags: [concept, modpack, download, manifesto]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [mods locais, jars servidos pelo servidor, manifesto reescrito]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[concepts/modside-rules]]"
  - "[[concepts/curseforge-proxy]]"
---

# Mods servidos pelo próprio servidor

> O launcher baixa os jars **do servidor TCMine**, não do CurseForge: o manifesto
> reescreve a URL de cada mod para `/files/{fileId}/{fileName}`.

## O que é

Os endpoints de modpack ([[entities/tcmine-server]] → `ModpackEndpoints`) entregam
ao launcher um manifesto cujos downloads apontam para o próprio servidor, que
mantém um cache compartilhado dos jars em `tcmine-data/mods/{fileId}/{fileName}`.

## Por que importa para o TCMine

- Controle e estabilidade: a disponibilidade dos jars não depende do CDN do
  CurseForge nem expõe o cliente ao CF (casa com [[concepts/curseforge-proxy]]).
- Consistência: todos os jogadores baixam exatamente o mesmo arquivo cacheado.

## Detalhes / Variações

- **`/api/modpacks`** — catálogo (só `IsPublished`), com contagem de mods do
  **lado cliente**.
- **`/api/modpacks/{uid:guid}`** — manifesto detalhado: filtra os mods com
  `ModSideRules.RunsOnClient` (ver [[concepts/modside-rules]]) e reescreve a URL
  para `{baseUrl}/files/{fileId}/{fileName}`. O `baseUrl` vem do `PublicBaseUrl`
  configurado (canônico atrás de proxy reverso) ou é derivado da requisição.
- **`/files/{fileId:long}/{fileName}`** — serve o jar do cache
  (`application/java-archive`); `Path.GetFileName` neutraliza path traversal.
- **`/api/modpacks/{uid}/overrides.zip`** — re-empacota sob demanda a pasta
  `tcmine-data/modpacks/{uid}/overrides` (fonte editável pelo painel).

## Aplicação concreta

- `TCMine-Server/Endpoints/ModpackEndpoints.cs`;
  `TCMine-Infrastructure/FileSystem/ServerPaths.cs` (`Mods`, `Modpacks`).

## Contradições / debates conhecidos

- O preenchimento do cache de jars (download a partir do CF no import) é feito
  pelo `ModpackImportService` — ainda a aprofundar nesta wiki.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
