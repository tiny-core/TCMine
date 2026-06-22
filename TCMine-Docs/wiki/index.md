---
type: index
title: Índice do Wiki TCMine-Docs
tags: [index]
updated: 2026-06-22
---

# Índice do Wiki

Catálogo curado de todas as páginas do wiki. É o mecanismo de recuperação
leve desta base (junto com `tools/wikisearch.py`). **Toda página nova deve ser
listada aqui** na seção correspondente, com um resumo de uma linha.

> Convenção: cada item é `- [[caminho/slug]] — resumo de uma linha (tags)`.

## Entidades (`wiki/entities/`)

> Projetos, componentes e artefatos concretos do ecossistema TCMine.

- [[entities/tcmine-solution]] — visão geral da solução: 7 projetos em Clean Architecture .NET 10 (overview, arquitetura)
- [[entities/tcmine-domain]] — entidades, enums e regras puras de domínio (domain, clean-architecture)
- [[entities/tcmine-application]] — portas, contratos (DTOs) e lógica pura de modpack (application)
- [[entities/tcmine-infrastructure]] — EF Core, CurseForge, filesystem e serviços (infrastructure, ef-core)
- [[entities/tcmine-design]] — design system compartilhado (`ColorTokens`) (design-system, theming)
- [[entities/tcmine-server]] — Blazor Server + Minimal API: backend + painel admin (blazor, backend)
- [[entities/tcmine-launcher]] — app desktop Avalonia, "a Steam do TCMine" (avalonia, launcher)
- [[entities/tcmine-icongenerator]] — gera ícones/assets para launcher e servidor (tooling, assets)

## Conceitos (`wiki/concepts/`)

> Ideias, padrões e decisões transversais (design tokens, render modes, etc.).

- [[concepts/clean-architecture]] — Domain → Application → Infrastructure, dependências para dentro (arquitetura)
- [[concepts/design-tokens]] — `ColorTokens` como fonte única para CSS, MudBlazor e Avalonia (design-system, theming)
- [[concepts/modside-rules]] — filtragem única cliente/servidor compartilhada no core (modpack, domain)
- [[concepts/modpack-mods-locais]] — jars servidos pelo próprio servidor, não pelo CurseForge (modpack, download)
- [[concepts/curseforge-proxy]] — CurseForge via proxy `/v1`; a key nunca sai do servidor (curseforge, segurança)
- [[concepts/secrets-data-protection]] — segredos cifrados em repouso via Data Protection (segurança, secrets)
- [[concepts/setup-auth-cookie]] — primeira execução, setup do Owner e auth por cookie (auth, setup, roles)
- [[concepts/persistence-dual-provider]] — SQLite/Postgres com `AppDbContext` abstrato + migrations por provider (ef-core, persistência)
- [[concepts/player-config-sync]] — sync de configs do jogador entre PCs por `(uuid, modpackId)` (player-config, sync)

## Fontes / Resumos (`wiki/sources/`)

> Uma página por fonte ingerida de `raw/` ou da leitura de código vivo.

- [[sources/2026-06-22-leitura-codigo-vivo]] — leitura inicial completa da solução TCMine (code-ref, arquitetura)

## Sínteses e páginas derivadas

> Comparações, análises e respostas arquivadas a partir do query workflow.

_(nenhuma página ainda)_

---

### Mapa rápido dos projetos da solução (referência)

Entidades-âncora que provavelmente ganharão páginas. Caminhos relativos à raiz
da solução (`P:\TCMine\`), irmã de `TCMine-Docs/`.

- `TCMine-Domain/` — camada de domínio (Clean Architecture). → [[entities/tcmine-domain]]
- `TCMine-Application/` — casos de uso / regras de aplicação. → [[entities/tcmine-application]]
- `TCMine-Infrastructure/` — implementações de infraestrutura. → [[entities/tcmine-infrastructure]]
- `TCMine-Design/` — design system compartilhado (`ColorTokens.cs`, tokens). → [[entities/tcmine-design]]
- `TCMine-Server/` — app Blazor (Server). → [[entities/tcmine-server]]
- `TCMine-Launcher/` — app Avalonia (desktop). → [[entities/tcmine-launcher]]
- `TCMine-IconGenerator/` — geração de ícones/assets. → [[entities/tcmine-icongenerator]]
