---
type: concept
title: Design Tokens
tags: [concept, design-system, theming, cores]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [design tokens, tokens de cor, ColorTokens]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-design]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
---

# Design Tokens

> Valores de design (cor) nomeados e centralizados em `ColorTokens`, consumidos
> por **três** renderizadores diferentes a partir da **mesma** fonte da verdade.

## O que é

Design tokens são as variáveis nomeadas do design system (hoje cor; no futuro
radius/tipografia) que substituem valores "mágicos" espalhados pela UI. Vivem em
[[entities/tcmine-design]] (`ColorTokens.cs`), marca base **#F97316**
(orange-500).

## Por que importa para o TCMine

Há três consumidores com tecnologias distintas que precisam da mesma identidade:

1. **CSS / Blazor Server** — `ColorTokens.ToCssBlock()` injeta variáveis em
   `:root[data-theme="dark|light"]`.
2. **MudBlazor (admin)** — `MudThemeFactory` (em [[entities/tcmine-server]]) monta
   `PaletteDark`/`PaletteLight` a partir dos tokens.
3. **Avalonia (launcher)** — `AvaloniaTheme` (em [[entities/tcmine-launcher]]) lê
   `ToCssVariables` e gera `Color`/`SolidColorBrush`.

Nenhum duplica valores — todos derivam de `ColorTokens`.

## Detalhes / Variações

- Escalas **agnósticas de tema**: `Primary` (laranja), `Secondary` (âmbar),
  `Accent` (azul-céu), cada uma com `Base`/`Hover`/`Active`.
- Tokens **por tema** (`Dark`/`Light`) com **chaves lógicas iguais** — só o valor
  muda. Permite trocar tema em runtime sem trocar de instância de tema.
- `Light` usa semânticos mais saturados para manter contraste AA sobre fundo claro.
- **[2026-06-29]** Os **neutros do `Dark`** (fundos/bordas/texto) passaram de tom **quente**
  (preto-amarronzado) para **frio/azulado** (`Page #0B0B14` … `BorderStrong #34344E`,
  `Text.Primary #E8E8F0`/`Secondary #94A3B8`), por preferência visual (paleta do launcher v1). A
  **marca laranja** (`Primary`/`Accent`) **não mudou**. Como tudo deriva daqui, launcher (Avalonia) e
  painel admin (MudBlazor) ficaram azulados juntos.

## Aplicação concreta

- `TCMine-Design/ColorTokens.cs`; consumidores: `MudThemeFactory.cs`,
  `AvaloniaTheme.cs`, e o layout Blazor.

## Contradições / debates conhecidos

- Tokens não-cor (radius, tipografia) ainda não estão no design system — hoje no
  `MudThemeFactory` (`DefaultBorderRadius=8px`, fonte `Inter`). Candidato a
  unificação.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
