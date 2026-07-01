---
type: entity
title: TCMine (solução)
tags: [entity, tcmine, overview, arquitetura]
status: wip
created: 2026-06-23
updated: 2026-06-23
aliases: [TCMine, solução, TCMine.slnx]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[entities/tcmine-design]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-icongenerator]]"
---

# TCMine (solução)

> Solução **.NET 10** (`TCMine.slnx`) em **Clean Architecture**: um servidor
> Blazor + Minimal API, um launcher desktop Avalonia, uma core compartilhada e
> ferramentas de apoio. "A Steam do TCMine".

## Visão geral

`TCMine` é um ecossistema para distribuir e jogar modpacks de Minecraft: o
**servidor** publica catálogo/manifestos de modpacks e serve os jars; o
**launcher** desktop instala e atualiza as instâncias do jogador. A lógica que
os dois lados precisam decidir igual vive no **core** compartilhado.

São **8 projetos** (com `TCMine-Launcher.Infrastructure` desde 2026-06-29 — ver
[[decisions/launcher-clean-architecture]]), organizados em camadas com dependências apontando para
dentro:

**Core**
- [[entities/tcmine-domain]] — entidades, enums e regras puras de domínio.
- [[entities/tcmine-application]] — portas (interfaces), contratos (DTOs `record`) e lógica pura de modpack.
- [[entities/tcmine-server-infrastructure]] — EF Core (SQLite/Postgres), CurseForge, filesystem, identidade e serviços.

**Entrega e suporte**
- [[entities/tcmine-design]] — design system compartilhado (`ColorTokens`), fonte única de cor.
- [[entities/tcmine-server]] — ASP.NET Core (Minimal API + Blazor Server): backend do launcher + painel admin.
- [[entities/tcmine-launcher]] — app desktop Avalonia (MVVM + ReactiveUI).
- [[entities/tcmine-icongenerator]] — console SkiaSharp que gera ícones/assets.

## Responsabilidades / Escopo

- **Build/props compartilhados:** `Directory.Build.props` fixa `net10.0`,
  `Nullable`/`ImplicitUsings`/`LangVersion latest` e metadados (Authors: Jocian
  de Souza Mendonça; Company: Tiny Core; licença GPL-3.0; repo `tiny-core/TCMine`).
- **Central Package Management:** `Directory.Packages.props` centraliza todas as
  versões NuGet — ver [[decisions/central-package-management]].
- **Stack por projeto** (versões atuais): EF Core 10.0.9 (SQLite + Npgsql
  Postgres 10.0.2), MudBlazor 9.5.0, Avalonia 12.0.4 + ReactiveUI.Avalonia
  11.3.8, SkiaSharp 3.119.4, FluentValidation 12.1.1, Blazilla 2.4.0.

## Decisões e estado atual

- **[2026-06-23]** Decisões arquiteturais transversais (cada uma com página própria
  conforme forem aprofundadas): [[concepts/clean-architecture]],
  [[concepts/shared-domain-logic]], [[concepts/dtos-as-records]],
  [[decisions/persistence-dual-provider]], [[concepts/curseforge-proxy]],
  [[concepts/modpack-mods-locais]], [[concepts/sse-content-sync]],
  [[concepts/setup-auth-cookie]], [[concepts/player-config-sync]],
  [[concepts/secrets-data-protection]], [[concepts/design-tokens]].
- **[2026-06-23]** Estado: servidor com backend + painel admin (Dashboard,
  Settings) funcionais; launcher ainda **scaffolded** (ver
  [[entities/tcmine-launcher]]); orquestração de instâncias de servidor Minecraft
  **modelada** (entidades + paths) mas ainda não operada.

## Relações

- Existe um projeto de **referência** (implementação completa v1.2.0) em
  `P:\TCMine-Launcher-bk` — usado como guia, reescrevendo limpo (ver `CLAUDE.md`).

## Pontos em aberto

- [ ] Páginas de `concepts/` e `decisions/` para as decisões listadas acima.
- [ ] CRUD admin de modpacks/usuários/releases além de Dashboard/Settings.
- [ ] Implementar a UI e o fluxo real do launcher.

## Referências

- Código: `TCMine.slnx`, `Directory.Build.props`, `Directory.Packages.props`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
