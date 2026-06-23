---
type: entity
title: TCMine-Launcher
tags: [entity, tcmine, avalonia, launcher, desktop]
status: stub
created: 2026-06-23
updated: 2026-06-23
aliases: [TCMine-Launcher, launcher, desktop]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-design]]"
  - "[[concepts/design-tokens]]"
---

# TCMine-Launcher

> App desktop **Avalonia 12** (MVVM + ReactiveUI), "a Steam do TCMine". Consome o
> [[entities/tcmine-server]] para instalar e atualizar instâncias do jogador.

## Visão geral

`TCMine-Launcher` (namespace `TCMine_Launcher`) é o cliente desktop. **Estado
atual: scaffolded** — a estrutura Avalonia está montada e o tema já está ligado,
mas a lógica de produto (catálogo, install, update) ainda não foi implementada.

## Responsabilidades / Escopo

- **Bootstrap (`Program.cs`/`App.axaml(.cs)`):** Avalonia classic desktop
  lifetime, `UsePlatformDetect`, fonte **Inter**, `UseReactiveUI`,
  developer tools em DEBUG.
- **MVVM (`ViewModels/`, `Views/`):** `ViewModelBase`, `MainWindowViewModel`
  (hoje só `Greeting = "Welcome to Avalonia!"` — scaffold do template),
  `MainWindow`, `ViewLocator`.
- **Theme (`Theme/AvaloniaTheme.cs`):** `ApplyTheme` lê `ColorTokens.
  ToCssVariables` e gera recursos Avalonia (`{Nome}Color` + `{Nome}Brush`, ex.
  `Primary500Brush`), com as **mesmas chaves** em dark/light. Ver
  [[concepts/design-tokens]] e [[entities/tcmine-design]].

## Decisões e estado atual

- **[2026-06-23]** Tema do launcher deriva da **mesma** fonte de cor do servidor
  (`ColorTokens`) — nada de paleta duplicada.
- **[2026-06-23]** Auto-update planejado via **Velopack**: o launcher consumirá o
  feed estático em `{PublicBaseUrl}/updates` (servido por
  [[entities/tcmine-server]]).

## Relações

- Consome [[entities/tcmine-server]] (catálogo/manifesto/jars). Usa
  [[entities/tcmine-design]] via `AvaloniaTheme`. Compartilha lógica de modpack do
  core ([[entities/tcmine-application]]).

## Pontos em aberto

- [ ] Implementar UI e fluxo real (catálogo, install/launch, update).
- [ ] Integrar Velopack (auto-update) e o build do launcher pelo servidor.
- [ ] Definir os ViewModels reais (substituir o scaffold).

## Referências

- Código: `TCMine-Launcher/Program.cs`, `App.axaml.cs`, `ViewModels/`, `Views/`,
  `Theme/AvaloniaTheme.cs`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
