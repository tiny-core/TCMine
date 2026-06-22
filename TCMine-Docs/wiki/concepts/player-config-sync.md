---
type: concept
title: Sync de configs do jogador entre PCs
tags: [concept, player-config, sync, minecraft, mojang]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [configs do jogador, player config sync, sync entre PCs]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-domain]]"
---

# Sync de configs do jogador entre PCs

> Settings de jogo (keybinds, shader/texturas, minimapa) guardadas por
> `(Uuid, ModpackId)` como um zip, repostas quando o jogador entra noutro PC.

## O que é

`PlayerConfigEntity` guarda, por jogador (UUID do Minecraft) e modpack, um zip de
configs. Endpoint `/players/{uuid}/configs/{modpackId}`:

- **GET (leitura)**: aberto — são settings de jogo, sem segredos.
- **PUT (escrita)**: exige `Authorization: Bearer <token Minecraft>` que pertença
  ao UUID, validado contra a Mojang por `MinecraftAuthService`. Limitado por taxa
  (política `configs`: 30/min por IP). Corpo até 25 MB.

Política de resolução de conflito: **last-write-wins** por `UpdatedAt`.

## Por que importa para o TCMine

Reproduz a conveniência de um launcher moderno: o jogador troca de máquina e
recupera suas preferências de jogo sem reconfigurar. Como a chave é o UUID e o
conteúdo são só settings, o risco é baixo — daí a leitura aberta e o
`MinecraftAuthService` ser **fail-open** (autoriza se a Mojang cair; só nega com
401/403 ou UUID divergente).

## Detalhes / Variações

- Validação de chave defensiva (alfanumérico/`-`/`_`, ≤80 chars).
- Cache do resultado de auth ~10 min (não bate na Mojang a cada PUT).
- `IPlayerConfigRepository.UpsertAsync` devolve o instante da gravação.

## Aplicação concreta

- `TCMine-Server/Endpoints/PlayerConfigEndpoints.cs`;
  `MinecraftAuthService`/`PlayerConfigRepository` em [[entities/tcmine-infrastructure]];
  `PlayerConfigEntity` em [[entities/tcmine-domain]].

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
