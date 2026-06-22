---
type: concept
title: CurseForge sempre via proxy do servidor
tags: [concept, curseforge, proxy, seguranca, api-key]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [proxy CurseForge, /v1, x-api-key]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-application]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/secrets-data-protection]]"
---

# CurseForge sempre via proxy do servidor

> O acesso ao CurseForge passa por um **proxy** em `/v1/*` no servidor, que injeta
> a `x-api-key`. A key **nunca sai do servidor** — o launcher não a conhece.

## O que é

A porta `ICurseForgeApi` tem duas implementações: o **servidor** usa a API direta
(`api.curseforge.com` com a key); o **launcher** usaria o proxy. O endpoint
`/v1/{**path}` é um passthrough genérico (GET/POST): encaminha método, query e
corpo, adiciona `x-api-key`, e espelha status/content-type/corpo do upstream.

## Por que importa para o TCMine

- **A key é segredo** e fica só no servidor (cifrada — ver
  [[concepts/secrets-data-protection]]). Distribuir a key no launcher a exporia.
- Permite que o launcher use a **busca/resolução** de mods do CF (UI de "adicionar
  mod manualmente") sem credenciais próprias.
- Complementa [[concepts/modpack-mods-locais]]: busca via proxy, download dos jars
  via `/files/...`.

## Detalhes / Variações

- Se o Owner ainda não configurou o token, o proxy responde **503** ("não configurado").
- Sem cache nem transformação: quem entende o JSON é o cliente; um 404 do CF chega
  como 404.
- O token vem de `ServerSettingsService.GetCfApiKeyAsync` (settings de runtime no banco).

## Aplicação concreta

- `TCMine-Server/Endpoints/CurseForgeProxyEndpoints.cs`;
  `CurseForgeApiClient` em [[entities/tcmine-infrastructure]];
  `ICurseForgeApi` em [[entities/tcmine-application]].

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
