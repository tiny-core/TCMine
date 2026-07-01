---
type: concept
title: Sync de conteúdo via SSE
tags: [concept, sse, sync, tempo-real]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [SSE, /events, ContentNotifier, sync de catálogo]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-launcher-infrastructure]]"
  - "[[concepts/modpack-mods-locais]]"
---

# Sync de conteúdo via SSE

> O servidor empurra um **contador de versão** por Server-Sent Events (`/events`);
> o launcher recarrega o catálogo quando recebe uma versão maior.

## O que é

`MapEventsEndpoint` ([[entities/tcmine-server]]) abre um stream `text/event-stream`
em `/events`. O `ContentNotifier` ([[entities/tcmine-server-infrastructure]]) é um
singleton que mantém uma `Version` incremental e a transmite aos subscritores.

> **Consumidor (launcher), 2026-06-29:** o servidor já fazia o `Bump()` em todas as mutações
> (`ModpackImportService` save/delete/metadata/connections/overrides, `ServerInstanceService`,
> `ModpackNewsService`), mas o launcher novo não consumia o `/events`. Agora a porta `IContentWatcher`
> ([[entities/tcmine-application]]) + impl `ContentWatcher` ([[entities/tcmine-launcher-infrastructure]])
> ligam-se ao stream, fixam a baseline e disparam ao receber versão diferente (em stream ou após
> reconectar); o shell recarrega o **catálogo** e o **modpack ativo** (metadados/servidores) e atualiza o
> indicador "Servidor ligado/indisponível".

> **Consumidor (launcher), 2026-07-01 — disponibilidade:** o mesmo evento passou a alimentar os
> **badges de indisponibilidade**. A shell cruza as instâncias instaladas com `/api/modpacks`
> (`ReconcileAvailabilityAsync`) e marca `ModpackMissing` nas que sumiram; o manifesto do ativo passou a
> distinguir **404 (modpack removido/despublicado)** de **exceção (servidor offline)**. `InstalledModpack`
> expõe flags reativas (`ModpackMissing`/`AutoJoinServerMissing`/`HasAvailabilityWarning`) que a Home e a
> lista de Instâncias mostram como badge. A lista de servidores do modpack ativo também se reconstrói a
> partir do manifesto fresco em cada evento. Ver [[entities/tcmine-launcher]].

## Por que importa para o TCMine

Em vez de o launcher ficar fazendo polling do catálogo, ele se inscreve uma vez e
reage só quando algo muda — barato e quase em tempo real.

## Detalhes / Variações

- O cliente liga-se, recebe `data: {version}` inicial e fixa-a como **baseline**;
  a cada versão **maior**, recarrega `/api/modpacks`.
- **Keep-alive:** a cada 25s sem novidade, envia um comentário SSE
  (`: keep-alive`) para atravessar proxies/firewalls que cortam conexões ociosas
  (e `X-Accel-Buffering: no` desliga o buffering do nginx).
- **Canal limitado (capacidade 1, `DropOldest`):** se o cliente está lento, só
  interessa a versão mais recente — descarta intermédias em vez de acumular.
- **Público, sem auth** (como `/api/modpacks`): não há segredos, só um contador.
- `ContentNotifier.Bump()` incrementa e notifica — chamado quando o conteúdo
  público muda (modpacks criados/editados/eliminados).

## Aplicação concreta

- `TCMine-Server/Endpoints/EventsEndpoints.cs`;
  `TCMine-Server.Infrastructure/Server/ContentNotifier.cs`.

## Contradições / debates conhecidos

- Como o CRUD admin de modpacks ainda está por fazer, os **chamadores** de
  `Bump()` nas mutações ainda não estão todos ligados — a infraestrutura de
  notificação existe; o disparo em cada alteração será fechado quando o CRUD
  chegar.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
