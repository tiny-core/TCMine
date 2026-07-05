---
type: concept
title: Sync de configs do jogador
tags: [concept, player-config, sync, minecraft, auth]
status: stable
created: 2026-06-23
updated: 2026-07-05
aliases: [player config sync, configs do jogador, sync entre PCs]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
  - "[[sources/2026-07-03-player-config-sync-completo]]"
  - "[[sources/2026-07-05-player-configs-admin-hardening]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[entities/tcmine-launcher]]"
  - "[[concepts/setup-auth-cookie]]"
  - "[[concepts/launcher-install-launch]]"
---

# Sync de configs do jogador

> As configs de jogo do jogador sincronizam entre PCs por `(uuid, modpackId)`,
> autenticadas com o **token Minecraft** do próprio jogador.

## O que é

Sistema **fim-a-fim** (servidor + launcher) que sincroniza as configs
**player-owned** de cada modpack, chaveado por `(uuid, modpackId)`, de forma
**incremental** — só os ficheiros que mudaram trafegam, nunca o conjunto inteiro
(para não sobrecarregar a rede). Os ficheiros vivem **descompactados em disco** no
servidor (`tcmine-data/player-configs/{uuid}/{modpackId}/`, via
`ServerPaths.PlayerConfigs`) ao lado de um **`.tcmine-manifest.json`** (caminho →
hash+tamanho). O [[entities/tcmine-launcher]] puxa no prepare e empurra ao fechar o
jogo.

**Diff por manifesto:** cliente e servidor comparam manifestos (SHA-256 por ficheiro).
No pull, o launcher baixa só os ficheiros cujo hash difere; no push, envia só os
novos/alterados + o manifesto completo (o servidor apaga o que saiu do manifesto).
Depois da 1ª sincronização, mexer numa tecla ou num waypoint transfere kBs, não o
cache de mapa inteiro.

**Porquê disco e não BD:** o conjunto pode incluir o **cache de mapa** (dezenas/
centenas de MB) — grande demais para blob em BD. Disco é consistente com o resto do
servidor (jars de mods, overrides e updates já vivem em `tcmine-data/`), serve/recebe
por streaming e permite o diff por ficheiro (cada ficheiro é uma entrada real).

## O que entra no diff (allowlist)

O **mesmo** `PlayerDataProfile` (`TCMine-Domain/Launcher/PlayerDataProfile.cs`) que
o [[concepts/launcher-install-launch]] usa para preservar ficheiros do jogador num
update de overrides — fonte única, sem duplicar a lista:

- `options.txt` — **keybinds** + FOV, render distance, GUI scale, volumes, idioma,
  resource pack selecionado;
- `optionsshaders.txt`, `shaderpacks/*.txt` — shader selecionado e suas configs;
- `config/xaero*`, `journeymap/config` — settings do minimapa;
- **cache de mapa e waypoints só dos servidores** — `XaeroWaypoints/Multiplayer*`,
  `XaeroWorldMap/Multiplayer*`, `journeymap/data/mp`.

**Só mundos multiplayer (servidor), não singleplayer local:** o cache dos mundos que o
jogador cria localmente (`data/sp`, `Singleplayer_*`) fica **de fora de propósito**
(decisão do usuário) — sincroniza-se o mapa do servidor atrelado ao modpack, não os
mundos privados. Como a instância é por modpack, o `data/mp` acumulado ali já é,
na prática, o(s) servidor(es) daquele modpack.

O resto de `config/` **pertence ao modpack** e fica de fora (sincronizá-lo
clobbaria updates). `servers.dat` também fica de fora (é gerado pelo
`ServersDatWriter` a partir dos servidores do modpack).

## Por que importa para o TCMine

O jogador troca de PC (ou atualiza o modpack) e reencontra suas teclas/opções por
modpack, sem precisar de conta no painel — a identidade é a própria conta Minecraft.

## Detalhes / Variações

> **Todas as três rotas são autenticadas** com o token Minecraft do próprio UUID
> (desde 2026-07-05; ver [[sources/2026-07-05-player-configs-admin-hardening]]). Antes,
> os dois GETs de leitura eram abertos — qualquer um que soubesse um `(uuid, modpackId)`
> baixava as configs alheias (UUID é público) e o `bundle` virava amplificador de banda.

- **`GET …/manifest`** — leitura **autenticada**. Serve o `.tcmine-manifest.json` atual
  (para o cliente diferenciar); `404` se ainda não há config. Rate-limited (`configs`).
- **`POST …/bundle`** — leitura **autenticada**. Corpo `{ paths: [...] }`; devolve um zip
  **só** com esses ficheiros (o que ao cliente falta no pull), por streaming a partir
  de um temporário auto-apagável. Guarda de zip-slip nos caminhos. Rate-limited.
- **`PUT …/push`** — escrita autenticada: exige `Authorization: Bearer <token>`
  (access token Minecraft) que **pertença ao UUID**, validado pelo `MinecraftAuthService`.
  Corpo = zip com os ficheiros novos/alterados **+ o `.tcmine-manifest.json` completo**;
  o servidor extrai os ficheiros, **apaga** os que saíram do manifesto e regrava o
  manifesto com o seu `UpdatedAt`. Streaming para `.tmp`, teto de **256 MB** por pedido
  (`MaxConfigBytes`), limite de corpo do Kestrel levantado por pedido, `413` se exceder.
  **Cota por conjunto**: rejeita (`413`) se o tamanho total declarado no manifesto exceder
  `PlayerConfigs:MaxSetMb` (default **1 GB**) — barra o enchimento de disco mesmo com token
  válido ou durante o fail-open. Devolve `{ updatedAt }`. Rate-limited (`configs`, 30/min por IP).
- **Auth das leituras (launcher):** o `PlayerConfigSync` manda `Authorization: Bearer` em
  todos os pedidos (o pull recebe o access token via `PullAsync(..., accessToken, ...)`,
  passado pelo `LaunchOrchestrator` a partir da sessão). Best-effort mantém-se: `401/403`
  no pull cai no `try/catch` e o launch segue com as configs locais.

## Gestão admin (tela)

Página **`/admin/players`** ("Configs dos jogadores", Owner/Admin) para **ver e limpar** o
que está em disco — a única forma, já que não há tabela (só filesystem). Lista os conjuntos
`(uuid, modpackId)` **agrupados por jogador** (`MudDataGrid` com grouping), cada um com
tamanho, nº de ficheiros e último sync (do manifesto); resolve `modpackId`→nome do modpack
(pastas sem modpack na BD aparecem como "órfã"). Ações: **apagar um conjunto** ou **tudo de um
jogador** (`BusyOverlay` + confirmação). Backed por `PlayerConfigAdminService` (Infrastructure,
scoped): varre `player-configs/`, mede tamanho (iterativo, pula reparse points) e apaga com
guarda anti path-traversal.
- **Validação do token (`MinecraftAuthService`):** consulta o perfil na Mojang
  (`api.minecraftservices.com/minecraft/profile`), compara o `id` com o UUID
  (normalizado: minúsculas, sem hífens), e **cacheia** ~10 min. Comportamento
  **fail-open**: se a Mojang estiver indisponível (5xx/rede), **autoriza** — são
  settings de jogo, sem segredos; só **nega** em 401/403 confirmado ou UUID
  divergente.
- **Lado launcher (`PlayerConfigSync`, infra):**
  - **Pull** (no `LaunchOrchestrator.PrepareAsync`, após os overrides): GET manifest;
    salta se `UpdatedAt` == `InstalledModpack.ConfigSyncedAt`. Senão calcula o manifesto
    local (SHA-256), pede via `bundle` só os ficheiros com hash diferente/ausente e
    extrai (guarda de zip-slip). **Não apaga** ficheiros locais (só adiciona/atualiza).
  - **Push** (na shell, ao fechar o jogo — `MainWindowViewModel.MonitorGameAsync` →
    `ILaunchOrchestrator.PushConfigsAsync`): calcula o manifesto local, compara com o do
    servidor e faz PUT `push` só com o que mudou (+ manifesto). Nada mudou → no-op.
    Grava o `updatedAt` devolvido em `ConfigSyncedAt` e persiste a instância.
  - **Feedback na UI:** ambos reportam status (ex.: "A baixar/enviar configurações do
    jogador (N ficheiros)…") via um `Action<string>` que a shell mostra na **mesma label
    de status da Home** (`LaunchStatus`, barra de launch) — o pull dentro do progresso do
    prepare, o push marshalado para a UI thread ao fechar o jogo.
  - Ambos são **best-effort**: servidor offline não parte o launch nem a UI.
- **Last-write-wins** por timestamp: `ConfigSyncedAt` marca o `UpdatedAt` remoto já
  aplicado. Quem faz push por último vence. O pull nunca apaga ficheiros locais (evita
  destruir algo criado localmente ainda não sincronizado); remoções propagam-se via
  push (o servidor reconcilia pelo manifesto).

## Aplicação concreta

- Contrato partilhado: `TCMine-Application/Contracts/PlayerConfig.cs`
  (`PlayerConfigManifest`, `PlayerConfigFileInfo`, `PlayerConfigBundleRequest`).
- Servidor (sem BD): `TCMine-Server/Endpoints/PlayerConfigEndpoints.cs` (manifest/
  bundle/push, streaming); `TCMine-Server.Infrastructure/FileSystem/ServerPaths.cs`
  (`PlayerConfigs`); `TCMine-Server.Infrastructure/Minecraft/MinecraftAuthService.cs`.
  A migration `DropPlayerConfigs` (dois providers) removeu a tabela antiga.
- Launcher: `TCMine-Launcher.Infrastructure/PlayerConfigSync.cs` (diff por manifesto,
  SHA-256, streaming via temporário), `LaunchOrchestrator.cs`;
  `TCMine-Domain/Launcher/{PlayerDataProfile,InstalledModpack}.cs` (`ConfigSyncedAt`);
  `TCMine-Launcher/ViewModels/MainWindowViewModel.Play.cs`.

## Contradições / debates conhecidos

- Sem **merge** de configs: em caso de edições concorrentes em dois PCs, o último
  push vence (last-write-wins). Suficiente para keybinds/opções; não há resolução
  de conflito por campo.
- **Custo de disco:** o cache de mapa continua a ocupar disco no servidor (o diff
  poupa **rede**, não armazenamento). Mitigado desde 2026-07-05 por uma **cota por conjunto**
  (`PlayerConfigs:MaxSetMb`, default 1 GB) no push e pela **tela `/admin/players`** para
  vigiar/limpar `tcmine-data/player-configs/`. O teto de 256 MB continua sendo por push.
- **Fail-open ainda vale para as leituras:** com os GETs agora autenticados, uma
  indisponibilidade da Mojang **reabre** temporariamente a leitura (o `MinecraftAuthService`
  autoriza no fail-open). Trade-off aceite (settings de jogo, sem segredos); a cota limita o
  dano no lado da escrita.
- **Hashing a cada sync:** pull e push recalculam SHA-256 dos ficheiros locais. Com
  muitos tiles de mapa é CPU não-trivial; aceitável para servidor de comunidade, mas é
  o candidato óbvio a otimizar (cache size+mtime) se pesar.
- **Consistência manifesto↔disco:** o servidor confia no manifesto que o cliente envia
  (não recalcula hashes). Uma falha parcial de push pode desalinhar; o próximo push
  completo reconcilia.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
- [[sources/2026-07-03-player-config-sync-completo]]
