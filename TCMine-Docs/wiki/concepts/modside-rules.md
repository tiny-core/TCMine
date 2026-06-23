---
type: concept
title: ModSide e ModSideRules
tags: [concept, modpack, domain]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [ModSide, ModSideRules, lado do mod, filtragem por lado]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-domain]]"
  - "[[concepts/shared-domain-logic]]"
  - "[[concepts/modpack-mods-locais]]"
---

# ModSide e ModSideRules

> A fonte **única** da verdade para "este mod vai para o cliente, para o servidor,
> ou para os dois".

## O que é

`ModSide` (Domain) é um enum: `Both` (default, valor 0), `Client`, `Server`.
`ModSideRules` expõe duas regras puras: `RunsOnClient(side)` (`Both`/`Client`) e
`RunsOnServer(side)` (`Both`/`Server`).

## Por que importa para o TCMine

Cliente e servidor precisam montar instâncias **diferentes** do mesmo modpack
(alguns mods são só-cliente, ex. shaders; outros só-servidor). Centralizar a
regra garante que o manifesto do launcher e a build de servidor decidam igual —
ver [[concepts/shared-domain-logic]].

## Detalhes / Variações

- **Distinto de `Target`** (mod/resourcepack/shaderpack), que é só a pasta de
  destino no cliente — `ModSide` é sobre *onde roda*, `Target` sobre *onde instala*.
- **Inferência automática:** `CurseForgeImporter.InferSide` usa o *server pack* do
  modpack — mod presente no pack ⇒ `Both`; ausente ⇒ `Client`. Sem server pack,
  assume `Both` e o admin ajusta.

## Aplicação concreta

- **Manifesto público** ([[entities/tcmine-server]] → `ModpackEndpoints`): só os
  mods com `RunsOnClient` entram no manifesto do launcher (ver
  [[concepts/modpack-mods-locais]]).
- **Dashboard:** as contagens cliente/servidor seguem a mesma regra (`Both` conta
  para os dois lados).
- **Build de servidor Minecraft:** consumirá `RunsOnServer` (pendente).
- Persistido como **texto** (`"Both"`/`"Client"`/`"Server"`) no banco.

## Contradições / debates conhecidos

- A build de instância de servidor que consome `RunsOnServer` ainda não foi
  implementada (orquestração modelada, não operada).

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
