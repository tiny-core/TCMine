---
type: concept
title: Design Tokens
tags: [concept, design-system, theming, cores]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [design tokens, tokens de cor, ColorTokens]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-design]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
---

# Design Tokens

> Valores de design (cores) nomeados e centralizados em `ColorTokens`, consumidos
> por três renderizadores diferentes a partir da mesma fonte da verdade.

## O que é

Design tokens são as variáveis nomeadas do design system (cor, e futuramente
espaçamento/tipografia) que substituem valores "mágicos" espalhados pela UI. No
TCMine vivem em [[entities/tcmine-design]] (`ColorTokens.cs`), marca base
**#F97316** (orange-500).

## Por que importa para o TCMine

Há **três consumidores** com tecnologias distintas que precisam da mesma
identidade visual:

1. **CSS / Blazor Server** — `ColorTokens.ToCssBlock()` injeta variáveis em
   `:root[data-theme="dark|light"]`.
2. **MudBlazor (admin)** — `MudThemeFactory` (em [[entities/tcmine-server]]) monta
   `PaletteDark`/`PaletteLight` a partir dos tokens.
3. **Avalonia (launcher)** — `AvaloniaTheme` (em [[entities/tcmine-launcher]]) lê
   `ToCssVariables` e gera `Color`/`SolidColorBrush`.

Nenhum duplica valores — todos derivam de `ColorTokens`.

## Detalhes / Variações

- Escalas **agnósticas de tema**: `Primary` (laranja), `Secondary` (âmbar),
  `Accent` (azul-céu).
- Tokens **por tema** (`Dark`/`Light`) com **chaves lógicas iguais** — só o valor
  muda. Isso permite trocar tema em runtime sem trocar de instância de tema.
- `Light` usa semânticos mais saturados para manter contraste AA sobre fundo claro.

## Aplicação concreta

- `TCMine-Design/ColorTokens.cs`; consumidores: `MudThemeFactory.cs`,
  `AvaloniaTheme.cs`, e o layout Blazor.

## Contradições / debates conhecidos

- Tokens não-cor (radius, fonte) ainda não estão no design system — hoje no
  `MudThemeFactory`. Candidato a unificação.

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
