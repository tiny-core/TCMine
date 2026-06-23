---
type: entity
title: TCMine-Design
tags: [entity, tcmine, design-system, theming, cores]
status: wip
created: 2026-06-23
updated: 2026-06-23
aliases: [TCMine-Design, design system, ColorTokens]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
  - "[[concepts/design-tokens]]"
---

# TCMine-Design

> Design system compartilhado: `ColorTokens` é a **fonte única de cor** para os
> três renderizadores do TCMine (CSS/Blazor, MudBlazor, Avalonia).

## Visão geral

`TCMine-Design` (namespace `TCMine_Design`) é uma camada pequena e sem
dependências: constantes de cor + helpers de conversão. Marca base **#F97316**
(orange-500). Ver o conceito em [[concepts/design-tokens]].

## Responsabilidades / Escopo

- **`ColorTokens` (estático):**
  - Escalas **agnósticas de tema** (iguais em dark/light): `Primary` (laranja),
    `Secondary` (âmbar), `Accent` (azul-céu) — cada uma com `Base`/`Hover`/`Active`.
  - Tokens **por tema** (`Dark`/`Light`) com os **mesmos nomes lógicos** —
    `Background`, `Text`, `Semantic` (success/warning/error/info + bg). `Light` usa
    semânticos mais saturados para manter contraste AA.
  - `ToCssVariables(dark)` → dicionário de variáveis (chaves iguais nos dois
    temas; só o valor muda). `ToCssBlock`/`ToCssBlockBoth` → blocos CSS para
    `:root[data-theme="dark|light"]`.

## Decisões e estado atual

- **[2026-06-23]** **Mesmas chaves lógicas** entre dark e light — permite trocar
  tema em runtime sem trocar a instância de tema (no Avalonia, atualizar
  `{DynamicResource}`; no MudBlazor, alternar `IsDarkMode`).
- **[2026-06-23]** Três consumidores derivam de `ToCssVariables`, sem duplicar
  valores: CSS/Blazor (`ToCssBlock`), MudBlazor (`MudThemeFactory` em
  [[entities/tcmine-server]]), Avalonia (`AvaloniaTheme` em
  [[entities/tcmine-launcher]]).

## Decisões e estado atual — débitos

- Tokens **não-cor** (radius, tipografia) ainda não vivem aqui — hoje o
  `MudThemeFactory` fixa `DefaultBorderRadius=8px` e fonte `Inter`. Candidato a
  unificação no design system.

## Relações

- Consumido por [[entities/tcmine-server]] (MudBlazor + CSS) e
  [[entities/tcmine-launcher]] (Avalonia). Concretiza [[concepts/design-tokens]].

## Pontos em aberto

- [ ] Levar radius/tipografia para o design system (hoje espalhados).

## Referências

- Código: `TCMine-Design/ColorTokens.cs`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
