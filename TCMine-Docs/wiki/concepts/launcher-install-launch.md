---
type: concept
title: Pipeline de install/launch do launcher
tags: [concept, launcher, install, launch, cmllib, neoforge]
status: wip
created: 2026-06-29
updated: 2026-06-29
aliases: [install launch, jogar, pipeline de lançamento, instalar modpack]
sources:
  - "[[sources/2026-06-29-launcher-install-launch]]"
related:
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-server]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/player-config-sync]]"
  - "[[decisions/auth-msal-launcher]]"
---

# Pipeline de install/launch do launcher

> O launcher baixa um modpack oficial do servidor e lança o Minecraft (NeoForge). **Uma instância por
> modpack**, derivada do manifesto. Sem instâncias manuais — o launcher é só-oficial e dependente do
> [[entities/tcmine-server]].

## O que é

Fluxo de "Jogar" (página Jogar ou botão do card de Modpacks):

1. **Manifesto** — `ApiClient.GetManifestAsync(modpackId)` → `ModpackManifestDto` (mods com URL já
   apontando para `{server}/files/...`, servidores, `HasOverrides`, `LoaderVersion`, `Minecraft`).
2. **Registo** — `RegisterFromManifest` cria/atualiza o `InstalledModpack` local (metadados;
   `instance.json` em `%AppData%/TCMine/instances/{modpackId}/`). Versão nova → reaplica overrides.
3. **Preparação** (`LaunchOrchestrator.PrepareAsync`, sem arrancar):
   - **NeoForge** via CmlLib (`NeoForgeInstaller.Install` + `InstallAndBuildProcessAsync`) —
     `GameLauncher`. Progresso traduzido em `LaunchProgress`.
   - **Mods** (`ModInstaller`): baixa os jars em falta para uma **cache** partilhada
     (`cache/mods`) e copia para `mods/`/`resourcepacks/`/`shaderpacks/` por `Target`. Em paralelo
     (gate 4). **Sem Sha1** (o `ModDto` do servidor não o traz; jars são do nosso servidor, confiáveis).
   - **Overrides** (`OverridesInstaller`): `GET /api/modpacks/{id}/overrides.zip`, extrai uma vez por
     versão; numa atualização preserva os ficheiros do jogador ([[concepts/player-config-sync]] — só os
     **padrões** de `PlayerDataProfile`, por snapshot/restore).
   - **`servers.dat`** (`ServersDatWriter`, fNbt): os servidores do modpack aparecem na lista
     multijogador; auto-join no 1º servidor via `MLaunchOption.ServerIp/Port`.
4. **Arranque** — `Process.Start` + captura de stdout/stderr (`GameLogCapture`); `GameRunStateStore`
   guarda `(modpackId, pid)` para detetar o jogo aberto ao reabrir o launcher.

## Por que importa para o TCMine

É a função central do launcher ("a Steam do TCMine"): com um clique, o jogador instala e entra no
modpack oficial, sem mexer em mods/loader/Java à mão.

## Detalhes / Variações

- **RAM/Java** (`LauncherSettings`, página Definições): RAM efetiva = override da instância →
  recomendação do modpack (`RecommendedRamMb`) → global; limitada a [1 GB, RAM física] (`SystemInfo`).
  Java vazio = o CmlLib deteta/instala.
- **Estado do botão** (card e Jogar): Instalar (não instalado) / Jogar (instalado e atual) / **Atualizar**
  (versão do servidor ≠ instalada). A **versão instalada** (`InstalledModpack.ManifestVersion`) é mantida
  separada da **versão mais recente** do servidor (registada num dicionário no shell, atualizada via SSE
  — [[concepts/sse-content-sync]]): o refresh NÃO sobrescreve a instalada, só os metadados de exibição
  (nome/descrição/servidores). Clicar em **Atualizar** reinstala (overrides reaplicam via gating) e, no
  fim, a versão instalada passa a ser a do servidor → o botão volta a "Jogar".
- **Estado de launch** vive na shell (`MainWindowViewModel.Play.cs`): `IsLaunching`, `LaunchPercent`,
  `LaunchStatus`, `LaunchLog`, `IsGameRunning`. Updates pós-jogo marshalados para a UI thread.

## Contradições / debates conhecidos

- **Só NeoForge** neste incremento; Forge/Fabric/Quilt dão erro amigável (loader não suportado ainda).
- **Sem sync de configs** do jogador (o servidor só tem `PUT` de configs; falta o `GET`) — o
  `LaunchOrchestrator` ainda não puxa/empurra configs. Ver [[concepts/player-config-sync]].

## Aplicação concreta

- `TCMine-Launcher/Services/{LaunchOrchestrator,GameLauncher,ModInstaller,OverridesInstaller,ServersDatWriter,InstanceStore,SettingsStore,GameRunStateStore,GameLogCapture,MinecraftServerPinger,SystemInfo}.cs`;
  `Models/{InstalledModpack,LaunchProgress,LauncherSettings,PlayerDataProfile}.cs`;
  `ViewModels/{MainWindowViewModel.Play,HomePageViewModel,SettingsPageViewModel,ModpacksPageViewModel}.cs`;
  `Views/{HomePageView,SettingsPageView,ModpacksPageView}.axaml`.

## Referências

- [[sources/2026-06-29-launcher-install-launch]]
