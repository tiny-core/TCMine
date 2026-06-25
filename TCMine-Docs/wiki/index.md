---
type: index
title: Índice do Wiki TCMine-Docs
tags: [index]
updated: 2026-06-24
---

# Índice do Wiki

Catálogo curado de **todas** as páginas do wiki. É o mecanismo de recuperação
leve desta base: o **primeiro arquivo lido** ao responder uma pergunta ou antes
de implementar algo. **Toda página nova deve ser listada aqui** na seção
correspondente, com um resumo de uma linha.

> Convenção: cada item é `- [[caminho/slug]] — resumo de uma linha (tags)`.

## Entidades (`wiki/entities/`)

> Projetos, componentes e artefatos concretos do ecossistema TCMine.

- [[entities/tcmine-solution]] — visão geral: 7 projetos em Clean Architecture .NET 10 (overview, arquitetura)
- [[entities/tcmine-domain]] — entidades, enums e regras puras de domínio (domain, clean-architecture)
- [[entities/tcmine-application]] — portas, contratos (DTOs `record`) e lógica pura de modpack (application)
- [[entities/tcmine-infrastructure]] — EF Core dual-provider, CurseForge, filesystem, serviços (infrastructure, ef-core)
- [[entities/tcmine-design]] — design system compartilhado (`ColorTokens`), fonte única de cor (design-system, theming)
- [[entities/tcmine-server]] — Blazor Server + Minimal API: backend + painel admin (blazor, backend)
- [[entities/tcmine-launcher]] — app desktop Avalonia (scaffolded), "a Steam do TCMine" (avalonia, launcher)
- [[entities/tcmine-icongenerator]] — gera ícones/assets para launcher e servidor (tooling, assets)

## Conceitos (`wiki/concepts/`)

> Ideias, padrões e convenções transversais.

- [[concepts/clean-architecture]] — Domain ← Application ← Infrastructure, dependências para dentro (arquitetura)
- [[concepts/shared-domain-logic]] — o que cliente/servidor decidem igual vive no core, sem duplicar (arquitetura, modpack)
- [[concepts/modside-rules]] — `ModSide`/`ModSideRules`: fonte única de filtragem cliente/servidor (modpack, domain)
- [[concepts/dtos-as-records]] — DTOs de fio são `record` imutáveis, nunca classes (convenção, dtos)
- [[concepts/design-tokens]] — `ColorTokens` como fonte única de cor para CSS, MudBlazor e Avalonia (design-system, theming)
- [[concepts/curseforge-proxy]] — CurseForge via proxy `/v1`; a `x-api-key` nunca sai do servidor (curseforge, segurança)
- [[concepts/modpack-mods-locais]] — jars servidos pelo próprio servidor, manifesto reescrito (modpack, download)
- [[concepts/modpack-admin-editor]] — UI Blazor de criar/editar modpack: mods, import CF, overrides com Monaco (modpack, admin, blazor)
- [[concepts/sse-content-sync]] — `/events` empurra um contador de versão; launcher recarrega o catálogo (sse, sync)
- [[concepts/setup-auth-cookie]] — primeira execução, setup do Owner, auth por cookie e papéis (auth, setup, roles)
- [[concepts/secrets-data-protection]] — segredos cifrados em repouso via Data Protection (segurança, secrets)
- [[concepts/player-config-sync]] — sync de configs por `(uuid, modpackId)` com token Minecraft (player-config, sync)
- [[concepts/async-feedback-overlay]] — modal não-fechável (`BusyService`/`BusyOverlay`) em toda operação async do painel (blazor, ux, feedback)

## Decisões (`wiki/decisions/`)

> Registros de decisão de arquitetura (ADR): contexto → decisão → consequências.

- [[decisions/persistence-dual-provider]] — SQLite/Postgres com `AppDbContext` abstrato + migrations por provider (ef-core, persistência)
- [[decisions/central-package-management]] — uma versão por pacote NuGet em toda a solução (build, nuget)
- [[decisions/mods-many-to-many]] — mods em N:N (`ModFile` + `ModpackMod`) em vez de FK 1:N (ef-core, modpack)

## Fontes / Resumos (`wiki/sources/`)

> Uma página por fonte ingerida de `raw/` ou da leitura de código vivo.

- [[sources/2026-06-23-leitura-codigo-vivo]] — leitura inicial dos 7 projetos da solução (code, arquitetura)
- [[sources/2026-06-24-modpack-admin-ui]] — construção da UI admin de modpacks + BlazorMonaco (code, modpack, admin)
- [[sources/2026-06-25-busy-overlay]] — overlay bloqueante de feedback async no painel (code, blazor, ux)
- [[sources/2026-06-25-mods-many-to-many]] — normalização de mods em N:N (code, ef-core, modpack)

## Sínteses e páginas derivadas

> Comparações, análises e respostas arquivadas a partir do query workflow.

_(nenhuma página ainda)_

---

### Mapa rápido dos projetos da solução (referência)

Caminhos relativos à raiz da solução (`P:\TCMine\`), irmã de `TCMine-Docs/`.

- `TCMine-Domain/` → [[entities/tcmine-domain]]
- `TCMine-Application/` → [[entities/tcmine-application]]
- `TCMine-Infrastructure/` → [[entities/tcmine-infrastructure]]
- `TCMine-Design/` → [[entities/tcmine-design]]
- `TCMine-Server/` → [[entities/tcmine-server]]
- `TCMine-Launcher/` → [[entities/tcmine-launcher]]
- `TCMine-IconGenerator/` → [[entities/tcmine-icongenerator]]
