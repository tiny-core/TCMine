---
type: concept
title: Proxy do CurseForge (descontinuado)
tags: [concept, curseforge, segurança, proxy, descontinuado]
status: descontinuada
created: 2026-06-23
updated: 2026-07-05
aliases: [curseforge proxy, proxy /v1, x-api-key]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
  - "[[sources/2026-07-05-refactor-p0-proxy-overrides]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[concepts/secrets-data-protection]]"
  - "[[concepts/shared-domain-logic]]"
---

# Proxy do CurseForge (descontinuado)

> **⚠️ Descontinuado em 2026-07-05.** O endpoint `/v1/*` **não existe mais**. A
> API do CurseForge **não é exposta** pelo servidor. Ver
> [[sources/2026-07-05-refactor-p0-proxy-overrides]].

## Por que foi removido

O proxy `/v1/{**path}` era um passthrough **público e sem autenticação** que
injetava a `x-api-key` do servidor — qualquer um na internet podia usá-lo como um
proxy CurseForge grátis e esgotar a cota da API. Ao revisar o código, constatou-se
que **nenhum consumidor de primeira parte o usava**:

- O **launcher** baixa os jars já cacheados de `/files/{fileId}/{fileName}`
  (ver [[concepts/modpack-mods-locais]]) e o catálogo de `/api/modpacks`. Nunca
  chamou `/v1`.
- O **painel admin** (Blazor **Server**) usa o `CurseForgeApiClient` **in-process**
  (injetado no `ModpackImportService`), que fala direto com `api.curseforge.com`
  injetando a key **por requisição**. Não há chamada HTTP a proteger.

Logo, o proxy era **código morto e superfície de ataque** ao mesmo tempo. Foi
**removido** (endpoint + `MapCurseForgeProxy` + referência `/v1` no `IsApiPath`).

## Como o CurseForge é acessado hoje

- **Servidor (admin):** `CurseForgeApiClient`
  ([[entities/tcmine-server-infrastructure]]) via `IHttpClientFactory`, com a
  `x-api-key` lida das settings cifradas ([[concepts/secrets-data-protection]]).
  A key **nunca** sai do servidor.
- **Launcher:** só consome os endpoints próprios do TCMine-Server
  (`/api/modpacks`, `/files`, `/events`, `/players`, `/updates`). O CurseForge é
  invisível para ele — vê apenas jars já resolvidos e cacheados.

## Aplicação concreta

- `TCMine-Server.Infrastructure/CurseForge/CurseForgeApiClient.cs` (o único ponto
  que fala com o CF hoje).
- ~~`TCMine-Server/Endpoints/CurseForgeProxyEndpoints.cs`~~ (removido).

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]] (design original)
- [[sources/2026-07-05-refactor-p0-proxy-overrides]] (remoção)
