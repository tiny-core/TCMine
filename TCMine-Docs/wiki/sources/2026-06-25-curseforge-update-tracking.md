---
type: source
title: Rastreio de origem CF e checagem de atualizações
tags: [source, code, curseforge, modpack]
status: ingested
created: 2026-06-25
updated: 2026-06-25
source-type: code
origin: "Pedido do usuário + código vivo (Domain/Application/Infrastructure/Server)"
feeds:
  - "[[decisions/curseforge-update-tracking]]"
  - "[[concepts/modpack-admin-editor]]"
  - "[[entities/tcmine-server-infrastructure]]"
related: []
---

# Rastreio de origem CF e checagem de atualizações

> O usuário quis: tabela própria vinculando o modpack TCMine à versão importada do CF,
> verificar/atualizar o modpack mesclando, e um botão de buscar atualizações dos mods —
> economizando API.

## Resumo

Criada a 1:1 `ModpackImportSourceEntity` (versão importada + cache de update). O
`CurseForgeImporter`/`ImportedModpackDto`/`DraftImportDto` passaram a carregar a origem
(projectId + fileId). `ModpackImportService` ganhou `GetImportSourceAsync`,
`CheckModpackUpdateAsync` (TTL 6h, reusa `GetLatestFileAsync`) e `CheckModUpdatesAsync`
(batch `latestFilesIndexes` + batch files). UI: banner de origem no editor (verificar/atualizar)
e botão "Buscar atualizações" na aba Mods com `ModUpdatesDialog` (seleção). Migração
`ModpackImportSource` nos dois providers.

## Pontos-chave

- **Economia**: `latestFilesIndexes` no `POST /v1/mods` → 1 batch dá o arquivo mais recente
  por (gameVersion, loader) de N mods; só os mudados disparam 1 batch de `files`.
- **TTL + on-demand**: não há checagem no load; cache do `latest` do modpack em 1 linha.
- Atualizar mods = troca no rascunho + zera hash (Save re-baixa); atualizar modpack =
  re-import + merge preservando Side/Target.

## O que alimentou na wiki

- Criou [[decisions/curseforge-update-tracking]].
- Atualizou [[concepts/modpack-admin-editor]], [[entities/tcmine-server-infrastructure]].

## Referências

- `ModpackImportService.cs`, `CurseForgeApiClient.cs`, `ModUpdatesDialog`.
