---
type: entity
title: TCMine (solução)
tags: [entity, tcmine, arquitetura, overview]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine, solução TCMine, ecossistema TCMine]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[concepts/clean-architecture]]"
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-design]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-icongenerator]]"
---

# TCMine (solução)

> Ecossistema de **launcher + servidor de Minecraft modded** em .NET 10 —
> launcher desktop tipo "Steam do TCMine" + backend que serve modpacks, mods e updates.

## Visão geral

A solução (`TCMine.slnx`, em `P:\TCMine\`) reúne sete projetos sob **.NET 10**,
organizados em **Clean Architecture** ([[concepts/clean-architecture]]). A ideia
central: um **launcher** (`TCMine-Launcher`) que instala o Minecraft, gerencia
mods e entra no servidor com um clique; e um **servidor** (`TCMine-Server`) que é
ao mesmo tempo backend do launcher (API) e painel admin (Blazor Server) para
gerenciar modpacks, usuários, releases e settings.

Metadados de pacote (`Directory.Build.props`): Author *Jocian de Souza Mendonça*,
Company *Tiny Core*, repo `https://github.com/tiny-core/TCMine`, licença GPL-3.0.

## Projetos da solução

| Projeto | Papel | Página |
|---|---|---|
| `TCMine-Domain` | Entidades e regras de domínio | [[entities/tcmine-domain]] |
| `TCMine-Application` | Portas (interfaces), contratos (DTOs), lógica pura | [[entities/tcmine-application]] |
| `TCMine-Infrastructure` | EF Core, CurseForge, FileSystem, serviços | [[entities/tcmine-infrastructure]] |
| `TCMine-Design` | Design system compartilhado (`ColorTokens`) | [[entities/tcmine-design]] |
| `TCMine-Server` | Blazor Server + Minimal API (backend + admin) | [[entities/tcmine-server]] |
| `TCMine-Launcher` | App desktop Avalonia | [[entities/tcmine-launcher]] |
| `TCMine-IconGenerator` | Gera ícones/assets para launcher e servidor | [[entities/tcmine-icongenerator]] |

## Decisões e estado atual

- **[2026-06-22]** **Central Package Management** (`Directory.Packages.props`):
  uma única versão de cada pacote NuGet em toda a solução; `.csproj` referenciam
  só o `Include`.
- **[2026-06-22]** **TargetFramework `net10.0`** com `Nullable` e `ImplicitUsings`
  habilitados em todos os projetos (via `Directory.Build.props`).
- **[2026-06-22]** Princípio: **lógica de modpack compartilhada** servidor↔launcher
  vive no core (ver [[concepts/modside-rules]], [[concepts/modpack-mods-locais]]).
- **[2026-06-22]** Docker: `compose.yaml` define o serviço `tcmine-server`
  (Dockerfile em `TCMine-Server/`).

## Relações

- Compõe-se de todas as entidades de projeto acima.
- Implementa [[concepts/clean-architecture]].

## Pontos em aberto

- [ ] Orquestração de instâncias de servidor Minecraft (start/stop, processo Java) — só persistência hoje (`ServerInstanceEntity`).
- [ ] Build do launcher pelo próprio servidor + feed Velopack (`/updates`) — entidade `ReleaseEntity` já existe.
- [ ] Conteúdo da UI do launcher (Avalonia) ainda mínimo (`MainWindow` placeholder).

## Referências

- Código: `raw/code-refs/2026-06-22-leitura-inicial-solucao.md`
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
