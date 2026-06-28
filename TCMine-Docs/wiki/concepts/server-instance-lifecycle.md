---
type: concept
title: Ciclo de vida de uma instância de servidor
tags: [concept, server-instance, docker, provisioning, cache]
status: stable
created: 2026-06-27
updated: 2026-06-27
aliases: [provisionamento de servidor, ServerProvisioner, DockerMinecraftManager]
sources: [[[sources/2026-06-27-server-instances-e-ux]]]
related: [[[decisions/server-instances-docker]], [[concepts/modside-rules]], [[concepts/modpack-mods-locais]], [[entities/tcmine-infrastructure]]]
---

# Ciclo de vida de uma instância de servidor

> De um modpack cadastrado a um servidor Minecraft rodando: provisionar o diretório, subir o container,
> reconciliar o status e medir presença.

## O que é

`ServerInstanceEntity` é um servidor Minecraft gerenciado, derivado de um modpack. O fluxo, todo na
camada `TCMine-Infrastructure/ServerInstances/`, tem quatro fases.

## Por que importa para o TCMine

É o ponto em que o conteúdo (modpacks/mods) vira um servidor de verdade, com controle total e sem
duplicar disco mesmo com muitas instâncias e packs grandes.

## Detalhes / Variações

### 1. Provisionamento (`ServerProvisioner`)

Monta `tcmine-data/servers/{id}/`:

- **Cache de loader compartilhado** (`ServerRuntimeInstaller` + `ServerRuntimeCacheEntity`): a instalação
  do loader (NeoForge) por tupla `(loader, versão, MC)` é feita **uma vez** sob
  `tcmine-data/server-cache/installed/{slug}/` (o pesado `libraries/`), reaproveitada por todas as
  instâncias. Validação por artefato (`libraries/`), não só pela pasta — auto-cura instalações quebradas.
- **Links** (`ILinkStrategy`): symlink no Linux/Docker, hardlink/cópia no Windows dev. Liga o
  `libraries/` do cache e os jars dos mods do **lado servidor** (filtro [[concepts/modside-rules]]) sem
  copiar bytes — 500+ mods viram ponteiros.
- **Configs** (`ServerConfigWriter`): `server.properties` (merge preservando edições), `eula.txt`,
  `user_jvm_args.txt` (Xms/Xmx + flags), listas de jogador. Overrides do modpack semeiam o `config/`
  na primeira provisão.
- Marca `ProvisionedAt`. Re-provisionar **remove o container existente** (criado com o comando do loader
  antigo) para o próximo start recriar com o estado novo.

### 2. Execução (`DockerMinecraftManager`)

Cria/inicia/para/remove o container, envia comandos pelo **stdin** (console) e transmite os **logs**.
O comando vem de `ServerRuntimeInstaller.ResolveLaunchArgs` (deriva do layout do install). Detalhes:

- **Trava de início** (`SemaphoreSlim` global): recusa iniciar dois servidores ao mesmo tempo.
- **Mods/dependências**: o jar do mod e suas dependências obrigatórias (resolvidas via CurseForge na
  adição) ficam em `mods/`.

### 3. Reconciliação de status (`ServerStatusReconciler`)

Serviço de fundo (a cada 15s, escopo próprio) + on-load (lista/detalhe): compara o estado conhecido
(Running/Starting) com o daemon. Container saiu → **Crashed**; sumiu → **Stopped**. Resolve o caso de
"o container caiu e o painel não percebeu".

### 4. Presença (`MinecraftServerPinger`)

Server List Ping (protocolo do jogo) na porta da instância → jogadores online/máximo. Exibido no
detalhe (polling) e na lista (coluna, ping paralelo). Host: `PublicAddress` ou loopback.

## Aplicação concreta

- `TCMine-Infrastructure/ServerInstances/`: `ServerProvisioner`, `ServerRuntimeInstaller`,
  `DockerMinecraftManager`, `DockerEnvironment`, `DockerServerJavaRunner`, `ServerStatusReconciler`,
  `MinecraftServerPinger`, `ServerInstanceService` (fachada do painel).
- `ServerPaths` define `servers/` e `server-cache/`. Ver [[decisions/server-instances-docker]].

## Contradições / debates conhecidos

- Detecção de "pronto" (boot completo do MC) é aproximada: o status vira Running quando o container sobe,
  não quando o MC termina de carregar; o ping confirma a presença.

## Referências

- [[decisions/server-instances-docker]], [[sources/2026-06-27-server-instances-e-ux]].
