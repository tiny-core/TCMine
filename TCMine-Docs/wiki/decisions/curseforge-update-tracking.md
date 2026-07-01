---
type: decision
title: Rastreio de origem CF + checagem de atualizações (modpack e mods)
tags: [decision, curseforge, modpack, atualizacoes, api-economy]
status: aceita
created: 2026-06-25
updated: 2026-06-25
deciders: [Jocian, Claude]
supersedes: []
superseded-by: []
sources:
  - "[[sources/2026-06-25-curseforge-update-tracking]]"
related:
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[entities/tcmine-server]]"
  - "[[concepts/modpack-admin-editor]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[decisions/mods-many-to-many]]"
---

# Rastreio de origem CF + checagem de atualizações (modpack e mods)

> Modpacks importados do CurseForge ganham uma tabela 1:1 que guarda a versão importada;
> dá para verificar atualizações (do modpack e dos mods) e aplicá-las mesclando — com
> uso econômico da API do CF.

## Contexto

O import do CF mesclava no rascunho mas **não registrava** de qual projeto/versão veio.
Não havia como saber se há versão nova nem atualizar. O usuário pediu isso, com atenção
ao **custo da API** do CurseForge e ao desempenho.

## Decisão

- **Tabela `ModpackImportSources`** (entidade `ModpackImportSourceEntity`, **PK = ModpackId**,
  1:1 com o modpack, cascade): `CurseProjectId`, `CurseProjectName`, `InstalledFileId`,
  `InstalledVersion`, `ImportedAt` + **cache** `LastCheckedAt`/`LatestFileId`/`LatestVersion`.
  Preenchida no import (o `CurseForgeImporter`/`ImportedModpackDto` passou a devolver
  `CurseProjectId` + `CurseFileId`) e gravada no **Guardar** (`SaveAsync` recebe a origem).
- **Checagem do modpack** (`CheckModpackUpdateAsync`): reusa `GetLatestFileAsync(projectId)`
  (1 chamada), compara com `InstalledFileId`, grava o cache. **TTL de 6h** evita rebater na
  API; o botão "Verificar atualização" passa `force=true`. **Atualizar** = re-importar a
  versão nova e mesclar (preserva `Side`/`Target`; overrides pendentes; nova versão no Save).
- **Checagem dos mods** (`CheckModUpdatesAsync`, **sob demanda**, sem cache no banco): usa o
  **`latestFilesIndexes`** do `POST /v1/mods` — **1 batch para N mods** dá o arquivo mais
  recente por (gameVersion, loader); depois **1 batch** `/v1/mods/files` só para os que
  mudaram (resolve url/nome). Botão "Buscar atualizações" na aba Mods → diálogo de seleção
  (`ModUpdatesDialog`) → aplica no rascunho (troca FileId/versão/url, zera o hash p/ re-baixar).

## Consequências

- **+** Economia de API: checagem de **todos** os mods custa ~2 chamadas (não 1/mod);
  modpack custa 1. TTL + on-demand evitam tráfego no load.
- **+** Sem cache de update por-mod no banco → nunca fica obsoleto; só o `latest` do modpack
  é cacheado (1 linha) para o badge.
- **+** Atualização preserva as escolhas do admin (merge por `modId`, ver
  [[decisions/mods-many-to-many]] e `ModSetMerge`).
- **−** Nova tabela + migração (`ModpackImportSource`) nos dois providers.
- **−** O `latest` do modpack só é populado após a primeira verificação (banner mostra a
  versão instalada até lá).

## Alternativas consideradas

- **Cache de update por-mod no banco** (badges sem clicar): rejeitado — mais complexo, pode
  ficar obsoleto, exigiria migração extra. Pode virar um job diário futuro.
- **1 chamada por mod** (`/v1/mods/{id}/files`): rejeitado — caro; `latestFilesIndexes` em
  lote resolve em 1 request.

## Referências

- [[sources/2026-06-25-curseforge-update-tracking]]
- Código: `TCMine-Domain/Entities/ModpackImportSourceEntity.cs`;
  `TCMine-Server.Infrastructure/CurseForge/CurseForgeApiClient.cs` (`GetLatestFileIndexesAsync`);
  `TCMine-Server.Infrastructure/Minecraft/ModpackImportService.cs`
  (`CheckModpackUpdateAsync`/`CheckModUpdatesAsync`/`GetImportSourceAsync`);
  `Components/Pages/Admin/Modpacks/` (banner no editor, `Panels/ModsPanel`, `Dialogs/ModUpdatesDialog`).
