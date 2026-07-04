---
type: source
title: Sync de configs do jogador — implementação completa (2026-07-03)
tags: [source, code, player-config, sync, launcher, server]
status: ingested
created: 2026-07-03
updated: 2026-07-03
source-type: code
origin: "código vivo: TCMine-Server (endpoints/ServerPaths/migrations) + TCMine-Launcher(.Infrastructure)"
feeds:
  - "[[concepts/player-config-sync]]"
  - "[[concepts/launcher-install-launch]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
---

# Sync de configs do jogador — implementação completa (2026-07-03)

> Completou-se o sistema de sync das configs player-owned: o servidor passou a
> **persistir e servir** o zip (antes o `byte[]` era descartado) e o launcher passou
> a **puxar/empurrar** as configs no ciclo de jogo.

## Resumo

Antes desta sessão o sistema era só esqueleto: `PlayerConfigEntity` não tinha
coluna de blob (o `UpsertAsync` recebia o zip e gravava só o `UpdatedAt`), só
existia o `PUT`, e o launcher não tinha nenhum código de sync. A pedido do usuário
("salvar as configs de teclas para não se perderem ao atualizar o modpack ou trocar
de PC"), completou-se o fluxo fim-a-fim, com escopo = **reusar o `PlayerDataProfile`**
e trigger **automático** (decisões do usuário nesta sessão).

**Pivots no meio da sessão (dois):**
1. **BD → disco.** A 1ª versão guardava o zip como blob em BD (coluna `Blob` + migration
   `PlayerConfigBlob`). O usuário optou por **disco** + **manter o cache do mapa** (100s
   de MB inviabilizam blob em BD). Reverteram-se as migrations de Blob, removeu-se a
   camada EF e migrou-se para ficheiros em disco.
2. **Zip inteiro → diff incremental + mapa só de servidor.** O usuário pediu para enviar
   **só o diff** (poupar rede) e restringir o cache de mapa aos **mundos multiplayer**
   (servidor do modpack), excluindo os singleplayer locais. Trocou-se o par GET/PUT de
   zip único por um protocolo **manifest + bundle + push** (SHA-256 por ficheiro) e
   estreitou-se o `PlayerDataProfile`.

Registo abaixo reflete o **estado final** (disco + diff).

## Pontos-chave

- **Contrato partilhado:** `TCMine-Application/Contracts/PlayerConfig.cs` —
  `PlayerConfigManifest` (UpdatedAt + `Files: {rel → {hash, size}}`),
  `PlayerConfigFileInfo`, `PlayerConfigBundleRequest`.
- **Servidor (sem BD):**
  - Ficheiros descompactados em `tcmine-data/player-configs/{uuid}/{modpackId}/` +
    `.tcmine-manifest.json`. Removidos `PlayerConfigEntity`, `IPlayerConfigRepository`,
    `PlayerConfigRepository`, DbSet e DI; migration `DropPlayerConfigs` largou a tabela.
  - `GET /manifest` (serve o manifesto), `POST /bundle` (zip só dos caminhos pedidos,
    streaming via temporário `DeleteOnClose`), `PUT /push` (autenticado: extrai o
    alterado, apaga o que saiu do manifesto, regrava o manifesto com o seu `UpdatedAt`;
    teto 256 MB, `IHttpMaxRequestBodySizeFeature`, `413`). Todos rate-limited (`configs`).
- **Launcher (`PlayerConfigSync`, infra):**
  - Diff por manifesto (SHA-256 via `SHA256.HashDataAsync`). `PullAsync`: GET manifest →
    salta se `ConfigSyncedAt` bate; senão `POST bundle` só do que difere e extrai (guarda
    anti zip-slip; não apaga local). `PushAsync`: compara manifestos e faz `PUT push` só
    do alterado + manifesto; no-op se nada mudou. Tudo por ficheiro temporário (streaming).
  - `LaunchOrchestrator`: pull no `PrepareAsync` (após overrides); `PushConfigsAsync`
    (porta `ILaunchOrchestrator`). `InstalledModpack.ConfigSyncedAt` (last-write-wins).
    Shell (`MainWindowViewModel.Play.cs`): push ao fechar o jogo, persistindo a instância.
- **Escopo (allowlist, `PlayerDataProfile`):** `options.txt`, `optionsshaders.txt`,
  `shaderpacks/*.txt`, `config/xaero*`, `journeymap/config`; cache/waypoints **só de
  servidor**: `XaeroWaypoints/Multiplayer*`, `XaeroWorldMap/Multiplayer*`,
  `journeymap/data/mp`. Singleplayer local (`data/sp`, `Singleplayer_*`), resto de
  `config/` e `servers.dat` ficam de fora.

## O que alimentou na wiki

- [[concepts/player-config-sync]] — reescrito para `stable`: storage em disco, protocolo
  de diff (manifest/bundle/push), cache de mapa só de servidor, last-write-wins.
- [[concepts/launcher-install-launch]] — resolvida a contradição "sem sync de configs".

## Referências

- `TCMine-Application/Contracts/PlayerConfig.cs`
- `TCMine-Server/Endpoints/PlayerConfigEndpoints.cs`
- `TCMine-Server.Infrastructure/FileSystem/ServerPaths.cs`
- `TCMine-Server.Infrastructure/Migrations/{Sqlite,Postgres}/*_DropPlayerConfigs.cs`
- `TCMine-Launcher.Infrastructure/{PlayerConfigSync,LaunchOrchestrator}.cs`
- `TCMine-Domain/Launcher/{PlayerDataProfile,InstalledModpack}.cs`
- `TCMine-Launcher/ViewModels/MainWindowViewModel.Play.cs`
