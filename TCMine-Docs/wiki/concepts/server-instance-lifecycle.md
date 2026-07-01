---
type: concept
title: Ciclo de vida de uma instância de servidor
tags: [concept, server-instance, docker, provisioning, cache]
status: stable
created: 2026-06-27
updated: 2026-06-27
aliases: [provisionamento de servidor, ServerProvisioner, DockerMinecraftManager]
sources: [[[sources/2026-06-27-server-instances-e-ux]]]
related: [[[decisions/server-instances-docker]], [[concepts/modside-rules]], [[concepts/modpack-mods-locais]], [[entities/tcmine-server-infrastructure]]]
---

# Ciclo de vida de uma instância de servidor

> De um modpack cadastrado a um servidor Minecraft rodando: provisionar o diretório, subir o container,
> reconciliar o status e medir presença.

## O que é

`ServerInstanceEntity` é um servidor Minecraft gerenciado, derivado de um modpack. O fluxo, todo na
camada `TCMine-Server.Infrastructure/ServerInstances/`, tem quatro fases.

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
- **Progresso detalhado (2026-07-01):** cada etapa reporta via `IProgress<string>` para o `BusyOverlay`,
  que agora mostra um **log de passos** (concluídos com check, o atual com spinner) em vez de só a última
  linha. O download do instalador do NeoForge reporta **bytes** (`X/Y MB · %`); a instalação do NeoForge
  (`java --installServer`) **transmite a saída ao vivo** (ver abaixo); o cache-hit diz "já em cache —
  reutilizando"; o link de mods mostra `n/total`. Convenção de coalescência no `BusyService`: mensagens
  com o mesmo rótulo antes de `" — "` (o detalhe que varia ao vivo) substituem a linha em vez de inundar o log.
- **Streaming do instalador (2026-07-01):** o `IServerJavaRunner` ganhou um parâmetro opcional
  `IProgress<string>? output`; o `DockerServerJavaRunner` segue os logs do container com `Follow=true`
  (stream multiplexado lido linha a linha), reportando ao vivo **e** acumulando a saída completa para o
  `JavaRunResult` (diagnóstico).
- **Fases silenciosas + heartbeat (2026-07-01):** o `--installServer` do NeoForge tem passos **longos e
  sem saída** (o RENAME/ART após "Splitting … files" processa milhares de classes calado, por minutos).
  Sem feedback, o overlay congela na última linha e "parece travado". Solução no `ServerRuntimeInstaller`:
  um **timer de heartbeat (2s)** publica a última linha + **tempo decorrido (mm:ss)**, então o `mm:ss` sobe
  mesmo nas fases mudas. A saída do instalador só atualiza a "última linha"; o timer é quem publica.
- **Timeout do runner (2026-07-01):** `DockerServerJavaRunner` cancela via CTS ligado ao `ct` após
  **30 min** (instalações reais levam poucos minutos); ao estourar, remove o container e lança
  `TimeoutException` clara — um travamento não prende o provisionamento para sempre.
- **Provisionamento durável e reconectável (2026-07-01):** a provisão deixou de rodar no circuito Blazor
  (onde um refresh de página a interromperia e o `DbContext` scoped seria descartado embaixo dela) e passou
  a um **`ProvisioningCoordinator`** (singleton): cada job roda numa **tarefa de fundo com escopo de DI
  próprio** (`IServiceScopeFactory`), guarda o log de passos em memória e emite `Changed`. A página de
  detalhe **dispara** via `Coordinator.Start(id, applyUpdate)`, **inscreve-se** no evento e renderiza o
  progresso num painel próprio — então um **refresh reconecta** ao progresso ao vivo (o job continua no
  servidor). O container do instalador ganhou **nome determinístico** (`tcmine-install-{slug}`) e o runner
  **remove um órfão de mesmo nome** antes de criar (retomar sem conflito). A instância fica marcada como
  `ServerInstanceStatus.Provisioning` (persistido, sem migration — coluna é string); no **boot**,
  `Coordinator.RecoverAsync()` (chamado no `Program.cs` após as migrations) **retoma** as que ficaram
  nesse estado (re-provisão limpa e idempotente). Em falha, o status volta a `Stopped` (não re-tenta em
  loop). O `BusyOverlay` continua para as operações curtas (load/start/stop/edit); só a provisão migrou
  para o coordenador. Ver [[concepts/async-feedback-overlay]].

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

- `TCMine-Server.Infrastructure/ServerInstances/`: `ServerProvisioner`, `ServerRuntimeInstaller`,
  `DockerMinecraftManager`, `DockerEnvironment`, `DockerServerJavaRunner`, `ServerStatusReconciler`,
  `MinecraftServerPinger`, `ServerInstanceService` (fachada do painel).
- `ServerPaths` define `servers/` e `server-cache/`. Ver [[decisions/server-instances-docker]].

## Contradições / debates conhecidos

- Detecção de "pronto" (boot completo do MC) é aproximada: o status vira Running quando o container sobe,
  não quando o MC termina de carregar; o ping confirma a presença.

## Referências

- [[decisions/server-instances-docker]], [[sources/2026-06-27-server-instances-e-ux]].
