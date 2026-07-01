---
type: source
title: Dashboard com medidores de recurso + home pública revampada
tags: [source, code, dashboard, metrics, home, mudblazor]
status: ingested
created: 2026-07-01
updated: 2026-07-01
source-type: code
origin: "código vivo — TCMine-Server + TCMine-Infrastructure"
feeds:
  - "[[entities/tcmine-server]]"
---

# Dashboard com medidores de recurso + home pública revampada

Implementação a pedido do usuário: medir e mostrar **uso de RAM, CPU e disco** com
anéis de progresso (referência visual: um `MudProgressCircular` estilo gauge), enriquecer
a dashboard com mais métricas/gráficos e melhorar a **home pública**.

## O que mudou

- **`TCMine-Infrastructure/Server/SystemMetricsService.cs`** — reescrito. Passou de
  métricas só-do-processo para **CPU/RAM/disco do host/contêiner**, cross-platform:
  - **CPU:** delta de `Process.TotalProcessorTime` ÷ (tempo de relógio × `ProcessorCount`).
    Stateful (lock protege a última amostra; o serviço é singleton partilhado).
  - **RAM:** `GC.GetGCMemoryInfo()` → `MemoryLoadBytes`/`TotalAvailableMemoryBytes`
    (honra limites de cgroup/Docker).
  - **Disco:** tamanho ocupado **só pela pasta de dados do projeto** (`tcmine-data`) — varredura
    recursiva iterativa pulando reparse points (não segue os links do server-cache), **cacheada 30s** —,
    sobre a **capacidade do drive** como total. Gauge rotulado "Dados". (Antes reportava o drive inteiro.)
    Helper `ServerPaths.Data(root)`.
  - `SystemSnapshot` reescrito: `WorkingSetBytes`, `ManagedHeapBytes`, `Threads`, `Uptime`,
    `ProcessorCount`, `CpuPercent`, `Ram`/`Disk` (`(Used,Total)`) + helpers `*Percent`/`*Gb`/`*Mb`.
- **`TCMine-Server/Program.cs`** — registo passou a
  `AddSingleton(new SystemMetricsService(dataRoot))` (ctor agora exige o root dos dados).
- **`.../Dashboard/Widgets/MetricGauge.razor(.cs/.css)`** — **novo** componente: medidor
  circular reutilizável (0–100%), cor por limiar (verde <70, atenção <90, crítico ≥90),
  rótulo central sobreposto, legenda opcional.
- **`.../Dashboard/Widgets/SystemStatusCard.razor(.cs)`** — três `MetricGauge` (CPU/RAM/
  Disco) no topo; tiles do processo; gráfico de histórico de memória mantido
  (`WorkingSetMb`).
- **`.../Dashboard/Widgets/ModDistributionCard.razor(.cs)`** — barras → **donut**
  (cliente/servidor/ambos) + legenda; API de gráficos do **MudBlazor 9**
  (`ChartSeries<double>` + `ChartLabels`; `ChartSeries<T>.Data` é `ChartData<T>`, atribuído
  por `double[]`).
- **`TCMine-Server/Components/Pages/Home.razor(.cs/.css)`** — wire real ao
  `ContentCatalog` (modpacks publicados + estado do launcher); hero com gradiente, cards de
  destaque e grade de modpacks. CSS escopado com `::deep` sob um wrapper `.home-page`.

## Notas / armadilhas

- **MudBlazor 9 removeu `InputData`/`InputLabels`** do `MudChart`; sem migração, o analyzer
  emite `MUD0002` e os dados iriam como atributo HTML (donut vazio). Confirmado no
  `MudBlazor.xml` do pacote 9.6.0 (só existem `ChartSeries`/`ChartLabels`/`ChartOptions`).
- **CSS escopado do Blazor** não atinge elementos renderizados por componentes-filhos
  (MudPaper/MudProgressCircular) — daí `::deep` a partir de um elemento próprio da página.
- **Razor + `v@expr`**: `v@pack.Summary.Version` é tratado como e-mail (não avalia).
  Usar `@($"v{...}")`.

## Verificação

- `TCMine-Server` compila **0 warning / 0 error**. Home pública validada no browser
  (hero, destaques, card de modpack com `v1.0.0`, MC/loader/mods, "1 servidor(es) online").
- Dashboard admin **não** verificada visualmente (atrás de login); widgets usam a mesma API
  MudBlazor já validada.
