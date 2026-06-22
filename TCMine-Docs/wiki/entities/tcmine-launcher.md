---
type: entity
title: TCMine-Launcher
tags: [entity, tcmine, avalonia, desktop, launcher]
status: stub
created: 2026-06-22
updated: 2026-06-22
aliases: [TCMine-Launcher, launcher]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-design]]"
  - "[[concepts/design-tokens]]"
  - "[[concepts/modpack-mods-locais]]"
---

# TCMine-Launcher

> App desktop **Avalonia** (Windows, .NET 10): "a Steam do TCMine" — instala o
> jogo, gerencia mods e entra no servidor com um clique.

## Visão geral

`TCMine-Launcher` (namespace `TCMine_Launcher`) é o cliente desktop. Hoje o
projeto está no **esqueleto Avalonia + ReactiveUI**: `Program` (bootstrap
Avalonia, Inter font, dev tools em Debug), `App` (cria `MainWindow` com
`MainWindowViewModel`), `ViewLocator`, `ViewModelBase`. A lógica de instalação/
mods ainda não está implementada — mas a base de domínio (importador, manifesto,
`ModSide`) já vive no core para o launcher reusar.

## Responsabilidades / Escopo (planejado + atual)

- **Atual**: shell Avalonia (`WinExe`), `app.manifest`, compiled bindings,
  `AvaloniaTheme` aplicando [[concepts/design-tokens]] como recursos Avalonia
  (lê de `ColorTokens.ToCssVariables`, mesma fonte do Blazor/MudBlazor).
- **Planejado** (inferido do core/servidor):
  - Consumir `/api/modpacks` e `/api/modpacks/{uid}` (manifesto do lado cliente).
  - Baixar jars de `/files/...` (não do CurseForge — [[concepts/modpack-mods-locais]]),
    verificar `Sha1`, montar a instância cliente.
  - Escrever `servers.dat` a partir dos `ServerEntryEntity`.
  - Sync de configs do jogador (PUT autenticado — [[concepts/player-config-sync]]).
  - Autoupdate via **Velopack** consumindo `/updates`.

## Decisões e estado atual

- **[2026-06-22]** UI ainda mínima (`MainWindow` placeholder); ReactiveUI como
  framework de MVVM; fonte Inter.
- **[2026-06-22]** Tema lido de [[entities/tcmine-design]] via `AvaloniaTheme`
  (chaves de recurso iguais em dark/light, troca em runtime com `{DynamicResource}`).

## Relações

- Cliente de [[entities/tcmine-server]].
- Usa [[entities/tcmine-design]] e (planejado) [[entities/tcmine-application]].

## Pontos em aberto

- [ ] Implementar instalação do Minecraft + loader, download/verificação de mods, launch.
- [ ] Integração Velopack (feed `/updates`, `PublicBaseUrl`).
- [ ] Login Microsoft/Mojang para obter token usado no sync de configs.

## Referências

- Código: `TCMine-Launcher/Program.cs`, `App.axaml.cs`, `Theme/AvaloniaTheme.cs`
- Fonte: [[sources/2026-06-22-leitura-codigo-vivo]]
