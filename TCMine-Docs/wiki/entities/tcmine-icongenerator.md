---
type: entity
title: TCMine-IconGenerator
tags: [entity, tcmine, tooling, assets, skiasharp]
status: wip
created: 2026-06-23
updated: 2026-06-23
aliases: [TCMine-IconGenerator, IconGenerator, gerador de ícones]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
---

# TCMine-IconGenerator

> Console **SkiaSharp** que gera os recursos visuais do TCMine (ícones, favicon,
> splash, og-image) a partir de um único logótipo, para manter a estética
> consistente entre launcher e servidor.

## Visão geral

`TCMine-IconGenerator` (namespace `TCMine_IconGenerator`) é uma ferramenta de
build/dev: roda sem argumentos (detecta a raiz do repo subindo pastas até achar
o `*.sln`) e escreve os assets direto nas pastas dos outros projetos.

## Responsabilidades / Escopo

- Renderiza o logótipo (cubo isométrico laranja) em vários tamanhos (16–256) via
  `Utility.RenderIcon`/`WriteIco`/`RenderSplash`/`RenderBanner`.
- **Launcher → `TCMine-Launcher/Assets/`:** `icon.png` (256), `icon.ico`
  (multi-res), `splash.png` (520×300, instalador Velopack).
- **Server → `TCMine-Server/wwwroot/`:** `favicon.ico` (16/32/48),
  `favicon.png` (32), `Images/logo.png` (256), `Images/og-image.png` (1200×630).

## Decisões e estado atual

- **[2026-06-23]** Fonte única do visual: os dois projetos recebem assets
  derivados do **mesmo** logótipo, gerados de forma reproduzível.

## Relações

- Escreve assets para [[entities/tcmine-launcher]] e [[entities/tcmine-server]].
- Complementa [[entities/tcmine-design]] (cor) no eixo de **imagens** (a paleta de
  cor é código; os ícones são gerados aqui).

## Pontos em aberto

- [ ] Documentar `Utility.cs` (render do cubo, composição do .ico) se for evoluir.

## Referências

- Código: `TCMine-IconGenerator/Program.cs`, `Utility.cs`
- Fonte: [[sources/2026-06-23-leitura-codigo-vivo]]
