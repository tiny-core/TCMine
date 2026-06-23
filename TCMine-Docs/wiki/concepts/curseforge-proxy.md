---
type: concept
title: Proxy do CurseForge
tags: [concept, curseforge, segurança, proxy]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [curseforge proxy, proxy /v1, x-api-key]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[concepts/secrets-data-protection]]"
  - "[[concepts/shared-domain-logic]]"
---

# Proxy do CurseForge

> O CurseForge é sempre acessado **através do servidor** (`/v1/*`); a `x-api-key`
> é injetada pelo servidor e **nunca sai** dele.

## O que é

`MapCurseForgeProxy` ([[entities/tcmine-server]]) expõe um catch-all
`/v1/{**path}` (GET/POST) que repassa método, query e corpo para
`api.curseforge.com/v1/...`, adicionando a `x-api-key`. É **passthrough
transparente**: espelha status, content-type e corpo do upstream (um 404 do CF
chega como 404), sem cache nem transformação.

## Por que importa para o TCMine

A key do CurseForge é um segredo. Se o launcher falasse direto com o CF,
precisaria embarcar a key (vazaria). Com o proxy, o cliente faz pesquisa/resolução
de mods (UI de adição manual) **através do servidor**, que é a única ponta que
conhece a key.

## Detalhes / Variações

- **Sem key configurada → 503** ("Token do CurseForge não configurado"): o
  Owner ainda não a definiu nas settings (ver
  [[concepts/secrets-data-protection]]).
- **Servidor (import) não usa o proxy:** o `CurseForgeApiClient`
  ([[entities/tcmine-infrastructure]]) fala direto com o CF, injetando a key lida
  das settings **por requisição** (a key pode mudar em runtime). O proxy é a porta
  do **cliente**.
- A mesma `ICurseForgeApi` abstrai os dois acessos (direto vs proxy) — ver
  [[concepts/shared-domain-logic]].

## Aplicação concreta

- `TCMine-Server/Endpoints/CurseForgeProxyEndpoints.cs`;
  `TCMine-Infrastructure/CurseForge/CurseForgeApiClient.cs`.

## Contradições / debates conhecidos

- O proxy ainda **não** entra na política de rate limiting (hoje só o PUT de
  configs do jogador é limitado) — o `Program.cs` cita isso como evolução futura.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
