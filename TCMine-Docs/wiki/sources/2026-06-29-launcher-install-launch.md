---
type: source
title: Launcher — instalar + lançar modpack (NeoForge) (2026-06-29)
tags: [source, code, launcher, install, launch, cmllib]
status: ingested
created: 2026-06-29
updated: 2026-06-29
source-type: code
origin: sessão de implementação (TCMine-Launcher) + referência P:\TCMine-Launcher-bk
feeds:
  - "[[concepts/launcher-install-launch]]"
  - "[[entities/tcmine-launcher]]"
---

# Launcher — instalar + lançar modpack (NeoForge) (2026-06-29)

Incremento das **funcionalidades** do launcher: baixar/instalar um modpack oficial e **lançar o
Minecraft** (NeoForge). Modelo simplificado vs backup — **só-oficial**, uma instância por modpack, sem
instâncias manuais/import/export.

## O que foi implementado

- **Pipeline** (Services/Models, portados/adaptados do backup): `LaunchOrchestrator`, `GameLauncher`
  (CmlLib + NeoForge), `ModInstaller` (cache, sem Sha1), `OverridesInstaller`
  (`/api/modpacks/{id}/overrides.zip`), `ServersDatWriter` (fNbt), `InstanceStore`, `SettingsStore`,
  `GameRunStateStore`, `GameLogCapture`, `MinecraftServerPinger`, `SystemInfo`; modelos
  `InstalledModpack`, `LaunchProgress`, `LauncherSettings`, `PlayerDataProfile`. Pacotes novos:
  `CmlLib.Core.Installer.NeoForge`, `fNbt`.
- **Shell como hub** (`MainWindowViewModel.Play.cs`): instâncias instaladas, instância ativa, estado de
  launch (progresso/log), `RegisterFromManifest`, `PlayActiveAsync`/`PlayModpackAsync`,
  deteção de jogo a correr.
- **Página Jogar** (`HomePageViewModel`/`HomePageView`): hero da ativa, botão grande
  Instalar/Jogar + progresso + cancelar, servidores (ping) e grelha das instaladas.
- **Definições** (`SettingsPageViewModel`/`SettingsPageView`): slider de RAM (até à RAM física) +
  caminho do Java, persistidos.
- **Cards de Modpacks**: botão Instalar/Jogar/Atualizar por estado (`ModpackListItem`), que abre a
  página Jogar e dispara o launch.

## Conceito derivado

- [[concepts/launcher-install-launch]] — o pipeline e as suas variações.

## Pendências

- Só **NeoForge**; outros loaders dão erro amigável.
- **Sem sync de configs** do jogador (falta `GET` no servidor).
- Sem página de **Instâncias** dedicada (a grelha de instaladas vive na página Jogar; o tab Instâncias
  continua placeholder).
- **Validação em execução** pendente (compila 0/0; falta lançar o jogo de facto end-to-end).
