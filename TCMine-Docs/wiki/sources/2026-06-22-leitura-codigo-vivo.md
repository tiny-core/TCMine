---
type: source
title: Leitura inicial do código vivo da solução TCMine
tags: [source, tcmine, code-ref, arquitetura]
kind: code-ref
status: summarized
created: 2026-06-22
source_date: 2026-06-22
origin: raw/code-refs/2026-06-22-leitura-inicial-solucao.md
author: Jocian de Souza Mendonça (código)
feeds:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-design]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-icongenerator]]"
---

# Leitura inicial do código vivo da solução TCMine

> Primeira ingestão de conteúdo do wiki: leitura completa da solução TCMine
> (`P:\TCMine\`) para semear entidades e conceitos a partir do estado atual do código.

## Origem

- **Tipo:** code-ref (leitura de código vivo — nada copiado para `raw/`)
- **Localização:** nota em `raw/code-refs/2026-06-22-leitura-inicial-solucao.md`
- **Código lido em:** `P:\TCMine\` (branch `master`)

## Resumo

A solução TCMine é um ecossistema de **launcher + servidor de Minecraft modded**,
escrito em **.NET 10**, organizado em **Clean Architecture**. O launcher
(`TCMine-Launcher`, Avalonia) funciona "como a Steam para o TCMine": instala o
jogo, gerencia mods e entra no servidor. O backend (`TCMine-Server`, Blazor
Server + Minimal API) é o que o launcher consome e o painel admin pelo qual
modpacks, usuários e settings são gerenciados.

O fio condutor do design é **compartilhar a lógica de domínio** (modpack,
loaders, lado cliente/servidor) no core, de modo que launcher e servidor tomem
decisões idênticas; e **centralizar pontos sensíveis no servidor** (key do
CurseForge, serving dos jars, secrets cifrados).

## Pontos-chave

- Core em três camadas: [[entities/tcmine-domain]], [[entities/tcmine-application]],
  [[entities/tcmine-infrastructure]] — ver [[concepts/clean-architecture]].
- Design system compartilhado por 3 renderizadores (CSS/Blazor, MudBlazor,
  Avalonia): [[concepts/design-tokens]] / [[entities/tcmine-design]].
- Mods servidos pelo próprio servidor: [[concepts/modpack-mods-locais]].
- Filtragem por lado cliente/servidor unificada: [[concepts/modside-rules]].
- CurseForge sempre via proxy do servidor: [[concepts/curseforge-proxy]].
- Primeira execução, setup e auth por cookie: [[concepts/setup-auth-cookie]].
- Banco com dois providers e contexts concretos: [[concepts/persistence-dual-provider]].
- Sync de configs do jogador entre PCs: [[concepts/player-config-sync]].

## O que isto muda no wiki

- Criou todas as páginas de entidade dos 7 projetos + [[entities/tcmine-solution]].
- Criou os conceitos transversais listados acima.
- Cross-links bidirecionais entre entidades e conceitos.
- Contradições com material anterior: nenhuma (primeira ingestão de conteúdo).

## Citações / trechos relevantes

> "O TCMine Launcher … Funciona como a Steam para o TCMine: instala o jogo,
> gerencia mods, entra no servidor com um clique." — `TCMine-Launcher.csproj`
