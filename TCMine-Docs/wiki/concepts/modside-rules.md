---
type: concept
title: ModSide e filtragem cliente/servidor
tags: [concept, modpack, domain, modside]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [ModSide, ModSideRules, lado do mod, cliente vs servidor]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[concepts/modpack-mods-locais]]"
---

# ModSide e filtragem cliente/servidor

> Regra única e compartilhada de "qual mod vai para o cliente vs para o servidor",
> para os dois lados decidirem igual.

## O que é

`ModSide` (enum no domínio: `Both`/`Client`/`Server`, default `Both`) marca em que
lado um mod roda. `ModSideRules` é a fonte da verdade da filtragem:
`RunsOnClient(side)` (`Both`|`Client`) e `RunsOnServer(side)` (`Both`|`Server`).

É distinto de `Target` (mod/resourcepack/shaderpack), que é só a pasta de destino
no cliente.

## Por que importa para o TCMine

Tanto o **launcher** (monta a instância cliente) quanto o **servidor** (monta a
instância de servidor) precisam decidir **igual** quais mods incluir. Por isso a
regra vive em [[entities/tcmine-domain]], não duplicada nos dois lados — coerente
com [[concepts/clean-architecture]].

## Detalhes / Variações

- **Inferência do lado** (`CurseForgeImporter.InferSide`): o manifesto lista os
  mods do *cliente*; o *server pack* (quando o autor publica um) contém o
  subconjunto do servidor. Mod no server pack ⇒ `Both`; ausente ⇒ `Client`; sem
  server pack ⇒ `Both` (admin ajusta manualmente).
- O **manifesto público** (`/api/modpacks/{uid}`) filtra com `RunsOnClient` — só
  mods do cliente vão para o launcher. O catálogo conta mods de cliente.

## Aplicação concreta

- `TCMine-Domain/Modpack/ModSideRules.cs`, `ModEntryEntity.Side`;
  `CurseForgeImporter.InferSide` em [[entities/tcmine-application]];
  `ModpackEndpoints` em [[entities/tcmine-server]].

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
