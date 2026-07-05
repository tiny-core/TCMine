---
type: source
title: Métricas do sistema globais + card de métricas por instância
tags: [source, code, dashboard, metrics, server-instance, docker, mudblazor]
status: ingested
created: 2026-07-05
updated: 2026-07-05
source-type: code
origin: "código vivo — TCMine-Server + TCMine-Server.Infrastructure"
feeds:
  - "[[entities/tcmine-server]]"
  - "[[concepts/server-instance-lifecycle]]"
related:
  - "[[sources/2026-07-01-dashboard-metrics-home]]"
---

# Métricas do sistema globais + card de métricas por instância

Implementação a pedido do usuário: **(1)** o card de status do sistema no dashboard admin deve mostrar
métricas **globais** (do host), não do processo do servidor; **(2)** adicionar **um card por instância de
servidor** com as métricas daquele servidor.

> **Substitui** parte da decisão registrada em [[sources/2026-07-01-dashboard-metrics-home]]: lá a CPU era
> deliberadamente só-processo e o disco só a pasta `tcmine-data`. Aqui ambos viram globais do host.

## Decisões (confirmadas com o usuário)

- **CPU e disco globais do host** (não só-processo / não só a pasta de dados). RAM já era global.
- **Um card para cada instância** cadastrada: rodando → medidores ao vivo; parada → card ocioso com status.

## O que mudou

- **`TCMine-Server.Infrastructure/Server/SystemMetricsService.cs`** — reescrito:
  - **CPU global**: `CaptureGlobalCpuPercent()` lê os contadores acumulados de CPU do host e calcula
    `(totalDelta − idleDelta) / totalDelta`. Cross-platform: **Linux** parseia a linha `cpu ` de
    `/proc/stat` (`idle = idle + iowait`; `total` = soma de todos os campos) — no contêiner esses
    contadores refletem o **host**, pois o accounting de CPU não é isolado por namespace; **Windows**
    usa P/Invoke `GetSystemTimes` (`total = kernel + user`, `idle` próprio; kernel inclui idle). Estado
    (`_lastCpuIdle`/`_lastCpuTotal`) protegido por lock; baseline lido no construtor.
  - **Disco global**: `CaptureDisk()` agora devolve `(TotalSize − AvailableFreeSpace, TotalSize)` do
    **drive inteiro** que hospeda `tcmine-data`. Removidos a varredura recursiva `DirectorySizeBytes`, o
    cache de 30s e os locks/campos associados.
  - `SystemSnapshot` perdeu o helper `DiskUsedLabel` (não mais necessário); mantém `CpuPercent`,
    `Ram`/`Disk` e os helpers de %/GB.
- **`.../Dashboard/Widgets/SystemStatusCard.razor`** — legenda da CPU "servidor"→"host"; medidor de disco
  relabelado "Dados"→"Disco", legenda agora `usado / total GB` do drive.
- **`TCMine-Server.Infrastructure/ServerInstances/ServerInstanceMetricsService.cs`** — **novo**. Singleton
  sem BD (fala com o `DockerEnvironment` e o filesystem), amostrável por Timer fora do circuito Blazor.
  - `SampleAsync`: lista os containers `tcmine-mc-*` em execução e, em paralelo, lê
    `GetContainerStatsAsync(Stream=false)` de cada um; devolve `IReadOnlyDictionary<Guid, ServerInstanceStats>`
    indexado pelo Id parseado do nome. **CPU** pela fórmula do `docker stats`
    (`cpuDelta/systemDelta × nº núcleos × 100`); **memória** = `Usage` − page cache (`inactive_file` em
    cgroup v2, `cache` em v1). `IProgress` síncrono (`FirstStatCapture`) evita a corrida do
    `SynchronizationContext` do `Progress<T>`.
  - `SampleDiskAsync(ids)`: **uso em disco** do diretório de cada instância (`servers/{id}`) — vale para
    **rodando E parada**. Varredura recursiva iterativa pulando reparse points (não segue os links do
    cache de runtime), **cacheada por instância (30s)** e rodada em `Task.Run` (não bloqueia o circuito).
  - Registro: `AddSingleton<ServerInstanceMetricsService>()` no `Program.cs` (agora também injeta
    `IHostEnvironment` para achar `ServerPaths.Servers`).
- **`.../Dashboard/Widgets/ServerInstancesMetricsCard.razor(.cs/.css)`** — **novo** widget autocontido
  (Timer de 5s). Cruza `ServerInstanceService.ListAsync()` (nome/modpack/status/RAM configurada) com os
  stats ao vivo e o uso em disco; renderiza **um `MudPaper` por instância** numa grade responsiva. Rodando
  → dois `MetricGauge` (CPU do container; **RAM sobre o `-Xmx`/`RamMb` configurado**, pois o container não
  tem limite de cgroup → o host inteiro não seria denominador útil). Parada → estado ocioso com o status.
  **Ambos** os estados exibem, num rodapé, o **uso em disco** do diretório da instância (`Storage` + valor
  em GB/MB adaptativo).
- **`.../Dashboard/Dashboard.razor`** — inclui `<ServerInstancesMetricsCard/>` abaixo do card de sistema.

## Notas / armadilhas

- **Intervalo de 5s** (não 2s como o card de sistema): cada `GetContainerStats` com `Stream=false` é uma
  leitura de ~1s no daemon (precisa de duas coletas para o delta de CPU); com N servidores em paralelo, 5s
  mantém a carga sob controle. Guarda `_sampling` evita sobreposição de rodadas.
- **Serviços scoped a partir do Timer**: o widget injeta o `ServerInstanceService` (scoped, usa
  `AppDbContext`) mas só o toca dentro de `InvokeAsync`/`OnInitializedAsync` (contexto do circuito), nunca
  direto no callback do Timer — evita concorrência no DbContext. O `ServerInstanceMetricsService` é
  singleton sem BD, então é seguro em qualquer contexto.
- **Denominador da RAM por instância**: os containers não recebem `HostConfig.Memory`, então
  `MemoryStats.Limit` = memória do host. Usar o `RamMb` (o `-Xmx`) como total dá um medidor com significado.

## Verificação

- `TCMine-Server` compila **0 warning / 0 error** (a API de stats do Docker.DotNet 3.125 — `CPUStats`,
  `PreCPUStats`, `MemoryStats`, `ContainerStatsParameters.Stream` — foi validada no build).
- Dashboard admin **não** verificada visualmente (atrás de login; os medidores por instância dependem de
  containers em execução no daemon). Widgets reusam o `MetricGauge` já validado.
