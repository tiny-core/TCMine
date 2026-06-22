---
type: entity
title: TCMine-Design
tags: [entity, tcmine, design-system, theming]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine-Design, design system]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[concepts/design-tokens]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
---

# TCMine-Design

> Design system compartilhado: a **única fonte da verdade** das cores da marca,
> consumida por Blazor (CSS), MudBlazor e Avalonia.

## Visão geral

`TCMine-Design` (namespace `TCMine_Design`) hospeda `ColorTokens.cs`: os tokens
de cor centralizados do TCMine. Cor de marca base **#F97316** (orange-500). É a
materialização do conceito [[concepts/design-tokens]].

## Responsabilidades / Escopo

- **Escalas agnósticas de tema** (a identidade da marca não muda entre claro/escuro):
  `Primary` (laranja), `Secondary` (âmbar/dourado), `Accent` (azul-céu), cada uma
  com shades + `Base`/`Hover`/`Active`.
- **Tokens por tema** `Dark` e `Light` com os **mesmos nomes lógicos** (só o valor
  muda): `Background` (Page/Default/Surface/Elevated/Border/BorderStrong), `Text`
  (Primary/Secondary/Disabled/OnPrimary), `Semantic` (Success/Warning/Error/Info + `*Bg`).
- **Geradores de saída**:
  - `ToCssVariables(bool dark)` → dicionário de variáveis CSS.
  - `ToCssBlock(...)` / `ToCssBlockBoth()` → blocos CSS para
    `:root[data-theme="dark|light"]` (Blazor Server).

## Decisões e estado atual

- **[2026-06-22]** Mesmas **chaves lógicas** em dark/light permitem alternar tema
  em runtime só trocando valores (sem trocar de instância de tema) — base para
  os 3 consumidores.
- **[2026-06-22]** Consumido por: `MudThemeFactory` (admin Blazor, ver
  [[entities/tcmine-server]]), `AvaloniaTheme` (launcher, ver
  [[entities/tcmine-launcher]]) e CSS direto. Nenhum deles duplica valores —
  todos leem de `ColorTokens`.
- **[2026-06-22]** `Text.OnPrimary` é o **mesmo** nos dois temas (texto escuro
  sobre o laranja da marca). Tokens semânticos do `Light` são mais saturados para
  manter contraste AA sobre fundo claro.

## Relações

- Implementa [[concepts/design-tokens]].
- Consumido por [[entities/tcmine-server]] e [[entities/tcmine-launcher]].

## Pontos em aberto

- [ ] Tokens não-cor (espaçamento, tipografia, radius) ainda fora do design system
  (radius "8px" e fonte Inter hoje ficam em `MudThemeFactory`).

## Referências

- Código: `TCMine-Design/ColorTokens.cs` (ver `raw/code-refs/2026-06-22-leitura-inicial-solucao.md`)
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
