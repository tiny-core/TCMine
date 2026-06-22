---
type: entity
title: TCMine-IconGenerator
tags: [entity, tcmine, tooling, assets]
status: stable
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine-IconGenerator, gerador de ícones]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-server]]"
---

# TCMine-IconGenerator

> Ferramenta de linha de comando que gera **todos os recursos visuais** do TCMine
> (ícones, splash, favicons, og-image) a partir de um único logótipo.

## Visão geral

`TCMine-IconGenerator` (namespace `TCMine_IconGenerator`) é um app console que
renderiza, com **SkiaSharp**, os assets do launcher e do servidor a partir do
mesmo logótipo (cubo isométrico laranja), para a estética ficar consistente.

## Responsabilidades / Escopo

- Detecta a raiz do repositório automaticamente (sobe procurando a `*.sln`);
  roda sem argumentos (Run no Rider) ou recebe a raiz como 1º argumento.
- Renderiza PNGs em vários tamanhos (`16/32/48/64/128/256`).
- **Launcher** → `TCMine-Launcher/Assets/`: `icon.png` (256), `icon.ico`
  (multi-res), `splash.png` (520×300, instalador Velopack).
- **Server** → `TCMine-Server/wwwroot/`: `favicon.ico` (16/32/48), `favicon.png`
  (32), `logo.png` (256), `og-image.png` (1200×630).
- Lógica de render em `Utility.cs` (`FindRepoRoot`, `RenderIcon`, etc.).

## Decisões e estado atual

- **[2026-06-22]** Um único gerador para **ambos** os projetos garante consistência
  visual; SkiaSharp é a dependência de render.

## Relações

- Produz assets para [[entities/tcmine-launcher]] e [[entities/tcmine-server]].

## Pontos em aberto

- [ ] (nenhum identificado nesta leitura)

## Referências

- Código: `TCMine-IconGenerator/Program.cs`, `Utility.cs`
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
