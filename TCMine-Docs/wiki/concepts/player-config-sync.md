---
type: concept
title: Sync de configs do jogador
tags: [concept, player-config, sync, minecraft, auth]
status: wip
created: 2026-06-23
updated: 2026-06-23
aliases: [player config sync, configs do jogador, sync entre PCs]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[concepts/setup-auth-cookie]]"
---

# Sync de configs do jogador

> As configs de jogo do jogador sincronizam entre PCs por `(uuid, modpackId)`,
> autenticadas com o **token Minecraft** do próprio jogador.

## O que é

`MapPlayerConfigEndpoints` ([[entities/tcmine-server]]) expõe a escrita das
configs do jogador como um blob (zip), chaveado por `(uuid, modpackId)` —
`PlayerConfigEntity` tem chave composta exatamente nesses dois campos.

## Por que importa para o TCMine

O jogador troca de PC e reencontra suas configs/keybinds/options por modpack, sem
precisar de conta no painel — a identidade é a própria conta Minecraft.

## Detalhes / Variações

- **`PUT /players/{uuid}/configs/{modpackId}`** — exige `Authorization: Bearer
  <token>` (access token Minecraft) que **pertença ao UUID**, validado pelo
  `MinecraftAuthService`. Corpo limitado a **25 MB**; persistido via
  `IPlayerConfigRepository.UpsertAsync`, devolvendo `updatedAt`. Limitado por taxa
  (política `configs`, 30/min por IP).
- **Validação do token (`MinecraftAuthService`):** consulta o perfil na Mojang
  (`api.minecraftservices.com/minecraft/profile`), compara o `id` com o UUID
  (normalizado: minúsculas, sem hífens), e **cacheia** ~10 min. Comportamento
  **fail-open**: se a Mojang estiver indisponível (5xx/rede), **autoriza** — são
  settings de jogo, sem segredos; só **nega** em 401/403 confirmado ou UUID
  divergente.

## Aplicação concreta

- `TCMine-Server/Endpoints/PlayerConfigEndpoints.cs`;
  `TCMine-Server.Infrastructure/Minecraft/MinecraftAuthService.cs`;
  `TCMine-Server.Infrastructure/Persistence/Repositories/PlayerConfigRepository.cs`.

## Contradições / debates conhecidos

- O endpoint atual implementa só o **PUT** (escrita). A **leitura** (GET) é
  descrita como aberta no comentário do código, mas **não há rota GET** neste
  arquivo — ainda pendente (ou servida por outro caminho a confirmar).

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
