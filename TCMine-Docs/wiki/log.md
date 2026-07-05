---
type: log
title: Log do Wiki TCMine-Docs
tags: [log]
updated: 2026-06-23
---

# Log

Registro cronológico **append-only** de tudo que acontece nesta base de
conhecimento: ingestões, lints, sínteses arquivadas e mudanças estruturais.
**Entradas novas vão no topo** (mais recente primeiro).

Cada entrada começa com um cabeçalho parseável:

```
## [YYYY-MM-DD] <tipo> | <Título>
```

`<tipo>` ∈ `setup` | `ingest` | `decisao` | `lint` | `sintese` | `meta`.

Estrutura sugerida do corpo:

- **Fonte:** caminho em `raw/` ou path de código vivo lido.
- **Páginas afetadas:** `[[entities/...]]`, `[[concepts/...]]`, `[[decisions/...]]`, `[[sources/...]]`.
- **Resumo:** o que mudou e por quê (PT-BR).
- **Pendências:** o que ficou em aberto.

---

## [2026-07-05] ingest | Refactor P1 — analisadores, dedup do isAsset e testes do core

- **Fonte:** código vivo, continuação da sessão de refactor (após o P0). Ver
  [[sources/2026-07-05-refactor-p0-proxy-overrides]].
- **Páginas afetadas:** [[sources/2026-07-05-refactor-p0-proxy-overrides]] (pendências),
  [[entities/tcmine-solution]] (novo projeto de teste), `index.md`.
- **Resumo:** (1) **Analisadores** do .NET ligados em `Directory.Build.props`
  (`AnalysisLevel=latest-Recommended` + `EnforceCodeStyleInBuild`), com um `NoWarn`
  curado para as regras ruidosas/de baixo valor (CA1848/CA1305/CA1707/CA1822/CA1816/
  CA1861/CA1716/CA5350) — sinal caiu de ~172 para ~36 warnings acionáveis. Corrigidas
  já 3 mecânicas (P/Invoke `LinkUnix` com `LPUTF8Str` p/ paths UTF-8 no Linux +
  supressão local do CA2101; `Marshal.SizeOf<T>`; `sealed`). (2) **Dedup** do `isAsset`
  no `Program.cs` — passou a usar a fonte única `IsAssetPath`. (3) Novo projeto
  **`TCMine-Tests`** (xUnit) com **39 testes** verdes sobre a lógica pura do core:
  `ModSideRules`, `ModSetMerge`, `CurseForgeImporter` (InferSide/ClassToTarget/
  ResolveDownloadUrl/BuildOverridesZip) e `ModLoaders.ParseId`.
- **Pendências:** backlog de analisadores (CA1859/CS0618/CA1873/CA1725 de baixo valor).
  Split futuro opcional de `ModpackUpdateService`/`ModFileCacheService`. Página
  `entities/` própria para o `TCMine-Tests` se o projeto crescer. Security headers (CSP).

## [2026-07-05] ingest | Dashboard: gráficos lado a lado + card de disco + jogadores online

- **Fonte:** pedido do usuário (chart mal formatado + implementar recomendações).
- **Páginas afetadas:** [[sources/2026-07-01-dashboard-metrics-home]], [[entities/tcmine-server]].
- **Resumo:** (1) reajuste do `SystemStatusCard`: removido o **medidor de rede** (rede é taxa, não
  %) — restam CPU/RAM/Disco (md=4). Os dois gráficos (memória e rede) saíram de dentro do paper de
  status e ganharam **papers próprios lado a lado** (`MudGrid` md=6, `Height="240px"`); o
  `SystemStatusCard` passou a ocupar a largura total do dashboard. Revertido o `ValueText` do
  `MetricGauge` e o `NetActivityPercent` (mortos após tirar o medidor de rede). (2) Novo widget
  **`DataDiskUsageCard`**: quebra do uso de disco do `tcmine-data`
  por área (cache de mods/modpacks/instâncias/cache de servidor/configs de jogador/feed do
  launcher), com barras relativas; varredura off-thread no init. Colocado ao lado do
  `RecentModpacksCard`. (3) **Jogadores online** no `ServerInstancesMetricsCard`: Server List Ping
  (via `ServerInstanceService.PingAsync`) das instâncias em execução, em paralelo, mostrando
  `Online / Max` por card. Cuidado: há dois `ServerPing` (Application vs Infrastructure); o correto
  é o da Infra `(int Online, int Max, string? Description)`. Build zero warnings, boot limpo.
- **Pendências:** verificação visual precisa de login admin. Outras sugestões em aberto: status do
  CurseForge, feed de erros recentes, ações rápidas.

## [2026-07-05] ingest | Dashboard: medidor+gráfico de rede; remoção do ModDistributionCard

- **Fonte:** pedido do usuário (revamp do card de status do sistema).
- **Páginas afetadas:** [[sources/2026-07-01-dashboard-metrics-home]], [[entities/tcmine-server]].
- **Resumo:** (1) **Rede** adicionada ao `SystemStatusCard`: o `SystemMetricsService` passou a
  amostrar a taxa de transferência (bytes/s recebidos+enviados) via `NetworkInterface`
  (delta entre amostras, como o CPU; no contêiner reflete o tráfego do servidor). Novo campo
  `Network` em `SystemSnapshot` + helpers `NetRecvMbps/NetSentMbps/NetTotalMbps`. O 4º medidor
  (antes um disco duplicado) virou o de rede; como rede não tem "máximo" fixo, o anel mostra a
  atividade relativa ao pico da janela e o centro/legenda a taxa real (`FormatRate`). O
  `MetricGauge` ganhou um `ValueText` opcional (centro não-percentual). A rede tem o **seu
  próprio gráfico** (separado da memória, que tem escala muito maior), com duas séries — ↓
  Recebido e ↑ Enviado (mesma escala MB/s) — e legenda. (2) **`ModDistributionCard`
  removido** — os KPIs (`DashboardKpis`) já mostram Cliente/Servidor/Ambos; os campos
  `ClientMods/ServerMods/SharedMods` permanecem (usados pelos KPIs). `RecentModpacksCard` passou
  a ocupar a largura toda.
- **Pendências:** removida a `<AdditionalFiles>` pendente do `ModDistributionCard.razor` no
  csproj (o Rider a tinha adicionado explícita; ao apagar o arquivo, quebrava o gerador Razor —
  CS2001). Verificação visual do dashboard precisa de login admin.

## [2026-07-05] meta | Fix: launcher não encontrava as Views (namespace + RootNamespace)

- **Fonte:** relato do usuário ("Not Found: Views.HomePageView") + investigação.
- **Páginas afetadas:** [[entities/tcmine-launcher]].
- **Sintoma:** o `ViewLocator` resolve a View pelo nome do VM
  (`FullName.Replace("ViewModel","View")` + `Type.GetType`). Um auto-fix da IDE (Rider,
  "sync namespace with folder") **dropou o root namespace** de 7 ViewModels + Views/axaml,
  gerando `namespace ViewModels`/`Views` em vez de `TCMine_Launcher.ViewModels`/`.Views`.
  Resultado: o locator procurava `Views.HomePageView` (inexistente) — as páginas não
  renderizavam.
- **Causa raiz:** o `TCMine-Launcher.csproj` (e o `TCMine-Tests.csproj`) **não definiam
  `<RootNamespace>`**. Como o nome do projeto tem hífen, a IDE derivava o root errado. Todos
  os outros projetos já tinham `RootNamespace` explícito — por isso só esses dois quebravam.
- **Fix:** (1) restaurados todos os namespaces/usings/`clr-namespace` para
  `TCMine_Launcher.ViewModels`/`.Views` (e testes → `TCMine_Tests.Modpack`); (2) adicionado
  `<RootNamespace>TCMine_Launcher</RootNamespace>` e `<RootNamespace>TCMine_Tests</RootNamespace>`
  aos dois csproj — blindagem contra reincidência. Build zero warnings, 39 testes verdes.
- **Pendências:** —

## [2026-07-05] lint | Fix do loop infinito do HomePageViewModel + auditoria de loops

- **Fonte:** código vivo — warning da IDE no `HomePageViewModel.ServerLoopAsync` + varredura.
- **Páginas afetadas:** [[entities/tcmine-launcher]].
- **Resumo:** `HomePageViewModel.ServerLoopAsync` era um `while (true)` sem `CancellationToken`
  (fire-and-forget no construtor) que nunca parava e morria em silêncio se um ping lançasse.
  Corrigido com o padrão do `ContentWatcher`: CTS + `IDisposable`, `while (!ct.IsCancellationRequested)`,
  `Task.Delay(ct)`, e `try/catch` por ciclo (falha transitória re-tenta; cancelamento encerra).
  O `MainWindowViewModel` (dono do `Home`) passou a descartá-lo no seu `Dispose`.
  **Auditoria da solução inteira:** nenhum outro loop infinito sem cancelamento — os demais
  loops de background (`ContentWatcher`, `LauncherAutoBuildService`, `ServerInstanceDetail`) já
  têm CTS+IDisposable; os timers do dashboard descartam; os `while(true)` restantes são bounded
  (undo, leitura de VarInt, wait do Docker); os fire-and-forget são one-shot. Sem `async void`.
- **Pendências:** —

## [2026-07-05] lint | Varredura completa de dead code (tipos, métodos, propriedades)

- **Fonte:** código vivo, varredura solicitada (IDE0051/IDE0052 p/ privados + grep de
  alcançabilidade p/ públicos).
- **Páginas afetadas:** [[entities/tcmine-application]], [[entities/tcmine-server-infrastructure]].
- **Resumo:** confirmado **zero** membros privados mortos (analisadores IDE). Removidos os
  públicos sem nenhum chamador:
  - **Tipos:** `ReleaseDto` e `OverrideFileDto` (records de contrato sem uso; o segundo ficou
    órfão ao remover o método abaixo).
  - **Métodos:** `ModpackOverridesService.ListOverrideFiles`/`GetOverrideLength`/
    `DeleteOverrideFolderAsync`, `ModpackImportService.ExistsAsync`,
    `UserService.UsernameExistsAsync`, `ServerInstanceService.RestartAsync`,
    `CurseForgeImporter.ImportSingleAsync`.
  - `ListOverrideFiles` era mencionado no log histórico de 2026-06-24 (quando a árvore migrou
    para carregamento lazy via `ListOverrideChildren`) — o método ficou obsoleto ali e agora
    foi removido; a entrada histórica permanece intacta (é registro append-only).
  - Build zero warnings, 39 testes verdes.
- **Pendências:** `ModSetMerge`+`MergeResultDto` e `CfManifestFileDto.Required` seguem no código
  por **decisão explícita do usuário** (turno anterior), apesar de sem consumidor de produção.
  Falsos positivos confirmados alcançáveis: endpoints (`MapXxx`), páginas Blazor (`@page`),
  snapshots/factories do EF (tooling/reflection).

## [2026-07-05] lint | Remoção de propriedades/DTOs de contrato não usados

- **Fonte:** código vivo, varredura de propriedades de DTO sem uso (a pedido do usuário).
- **Páginas afetadas:** [[entities/tcmine-application]] (contratos).
- **Resumo:** removido dead code de `TCMine-Application/Contracts`: (1) o record **`NewsDto`**
  inteiro (zero referências — os feeds usam `NewsItemDto`/`NewsRowDto`); (2)
  `VersionOptionDto.**IsStable**` (alias `=> IsRelease` nunca lido); (3) **`ModpackUpdateStatusDto`**
  inteiro — o `CheckModpackUpdateAsync` retornava esse DTO rico mas o único consumidor só o
  null-checava (o banner lê tudo do entity `ModpackImportSourceEntity`); o método passou a
  retornar `bool` e o `ToStatus` foi apagado. Build zero warnings, 39 testes verdes.
- **Pendências (avaliadas, NÃO removidas por decisão do usuário):** `ModSetMerge`+`MergeResultDto`
  (sem consumidor de produção, só testes — mas listado como lógica compartilhada intencional) e
  `CfManifestFileDto.Required` (espelha o schema de fio do CF, nunca lido).

## [2026-07-05] ingest | Split do ModpackImportService em ModFileCacheService + ModpackUpdateService

- **Fonte:** código vivo, item 1 do backlog (continuação do split de responsabilidades).
- **Páginas afetadas:** [[concepts/modpack-admin-editor]], [[entities/tcmine-server-infrastructure]],
  [[sources/2026-07-05-refactor-p0-proxy-overrides]].
- **Resumo:** o `ModpackImportService` (794 linhas) foi dividido em três serviços com
  responsabilidade única: **`ModFileCacheService`** (cache de jars/SHA-1, marcação de
  órfãos, upload manual, server pack), **`ModpackUpdateService`** (checagem de
  atualizações CF do modpack e mods — leitura pura, consumida só pela UI) e o
  `ModpackImportService` reduzido (busca/add/import/save do rascunho). O import injeta o
  cache (EnsureCached/LoadServerPack/MarkOrphans); os consumidores da UI (`ModpackEditor`,
  `Mods`) foram migrados para os novos serviços. 3 registros DI novos (scoped). Build
  **zero warnings**, 39 testes verdes, e o **boot em Development valida o grafo de DI**
  (`ValidateOnBuild`) — servidor sobe limpo.
- **Pendências:** E2E autenticado do editor de modpack (render das páginas admin — precisa
  de login, mesma limitação do Monaco). Split de serviços do modpack agora **concluído**.

## [2026-07-05] ingest | Build a zero warnings (fim do backlog de analisadores)

- **Fonte:** código vivo, limpeza final dos analisadores.
- **Páginas afetadas:** [[sources/2026-07-05-refactor-p0-proxy-overrides]].
- **Resumo:** solução compila **sem nenhum warning**. Corrigidos: **CS0618** (SkiaSharp
  4.x — `SKPath` in-place obsoleto → `SKPathBuilder` no IconGenerator) e **CA1859** ×4
  (tipos concretos onde não há custo de design: retorno `HttpClientHandler` nos
  `CreateHandler`; params `List<long>`/`Dictionary<Guid,string>` em métodos privados).
  **Suprimidos** com justificativa no `Directory.Build.props`: **CA1873** (boxing em log,
  mesma família do CA1848 já suprimido) e **CA1725** (renomear param do `OnModelCreating`
  — churn cosmético). 39 testes seguem verdes.
- **Pendências:** E2E do editor Monaco autenticado; split opcional de
  `ModpackUpdateService`/`ModFileCacheService`; testes do manifesto de player-config.

## [2026-07-05] decisao | Security headers (CSP) no painel, validado contra o app

- **Fonte:** código vivo, item de segurança do backlog P1.
- **Páginas afetadas:** [[concepts/security-headers]] (nova), [[entities/tcmine-server]],
  `index.md`.
- **Resumo:** novo middleware `SecurityHeaders` (`app.UseSecurityHeaders()`) aplica CSP +
  `X-Content-Type-Options`/`X-Frame-Options`/`Referrer-Policy`/`Permissions-Policy` a todas
  as respostas. A CSP foi **calibrada e verificada contra o app rodando** (home + login
  MudBlazor + scripts do Monaco + WebSocket do Blazor): `style-src 'unsafe-inline'`
  (tokens/MudBlazor), `script-src/worker-src blob:` (workers do Monaco), `img-src https:`
  (thumbnails CF), `connect-src 'self'` (Blazor WS/SSE). Zero violações no console. Header
  CSP mantido **único** via `OnStarting`+indexer (o framework Blazor emitia um segundo
  `frame-ancestors 'self'`).
- **Pendências:** testar o editor Monaco autenticado (abrir overrides) num ambiente com
  login — os scripts carregam, falta o E2E do worker em edição real.

## [2026-07-05] ingest | Fecho do backlog de analisadores (CA1068/CA1001/CA2016) + CI

- **Fonte:** código vivo, continuação do P1.
- **Páginas afetadas:** [[sources/2026-07-05-refactor-p0-proxy-overrides]], `index.md`.
- **Resumo:** (1) **CA1068** — `CancellationToken` reordenado para último em
  `GameLauncher.PrepareAsync` e `ModInstaller.EnsureModsAsync` (+ chamadas no
  `LaunchOrchestrator`). (2) **CA1001** — `IDisposable` implementado nos 7 tipos com
  campos descartáveis: `ContentWatcher` (+ fix de leak no `Stop`), `ModpackCatalog`,
  `NewsFeed` (HttpClient próprio), `GitHubReleaseService`, `ServerSettingsService`
  (SemaphoreSlim de singleton), `LauncherAutoBuildService` (CTS do hosted service) e
  `MainWindowViewModel` (CTS do launch). (3) **Bônus — CA2016** expôs um bug real: o
  install do CmlLib (`InstallAndBuildProcessAsync`) não recebia o token, então o
  `CancelLaunch` não interrompia o download; corrigido. (4) Nova workflow **`ci.yml`**
  (build da solução + `dotnet test` em push/PR na master). 39 testes verdes.
- **Pendências:** 18 warnings de baixo valor (CA1859 perf, CS0618 SkiaSharp obsoleto no
  IconGenerator, CA1873, CA1725). Security headers (CSP) como próximo item de segurança.

## [2026-07-05] decisao | Refactor P0 — remoção do proxy CurseForge + split do ModpackImportService

- **Fonte:** código vivo, sessão de análise completa da solução + refactor P0. Ver
  [[sources/2026-07-05-refactor-p0-proxy-overrides]].
- **Páginas afetadas:** [[concepts/curseforge-proxy]] (→ descontinuado),
  [[concepts/modpack-admin-editor]], [[sources/2026-07-05-refactor-p0-proxy-overrides]].
- **Resumo:** (1) O proxy CurseForge `/v1/*` foi **removido** — era público, sem auth
  nem rate limiting, injetando a `x-api-key`, e **nenhum consumidor o usava** (launcher
  baixa de `/files`; admin usa o `CurseForgeApiClient` in-process). Código morto +
  buraco de segurança eliminados. (2) O monolito `ModpackImportService` (1223 linhas)
  foi dividido: extraído `ModpackOverridesService` (~430 linhas: edição de overrides +
  histórico/desfazer), sobrando 794 linhas com responsabilidade única. Build verde,
  consumidores e DI atualizados. Decisão do proxy confirmada com o usuário.
- **Pendências:** P1 — testes do core, dedup do `isAsset` no `Program.cs`, ligar
  analisadores. Possível split futuro de `ModpackUpdateService`/`ModFileCacheService`.

## [2026-07-05] ingest | Reorganização do TCMine-Launcher.Infrastructure em pastas por área de domínio

- **Fonte:** código vivo, a pedido do usuário (arquivos todos na raiz → organizar como o server infra).
- **Páginas afetadas:** [[sources/2026-07-05-launcher-infra-folders]] (nova),
  [[entities/tcmine-launcher-infrastructure]] (seção Organização + frontmatter), `index.md`.
- **Resumo:** os ~21 arquivos da infra do launcher saíram da raiz para **9 pastas por área de domínio**
  (`Auth/`, `Configuration/`, `Content/`, `FileSystem/`, `Launch/`, `Networking/`, `Persistence/`,
  `Platform/`, `Updates/`), com **namespace casando** (`TCMine_Launcher.Infrastructure.<Pasta>`),
  espelhando o `TCMine-Server.Infrastructure`. `git mv` + usings explícitos por arquivo; 2 consumidores
  no projeto UI ajustados. Refactor puramente estrutural; solução compila 0 erro.
- **Pendências:** nenhuma. (`SystemInfo` foi para `Platform/`, não `System/`, para não colidir com o
  namespace `System`.)

## [2026-07-05] ingest | Tela admin de configs dos jogadores + endurecimento da API de sync

- **Fonte:** código vivo, a pedido do usuário (tela para gerir configs de player + dúvida sobre a segurança
  da API de sync). TCMine-Server + Infrastructure + Launcher.Infrastructure.
- **Páginas afetadas:** [[sources/2026-07-05-player-configs-admin-hardening]] (nova),
  [[concepts/player-config-sync]] (reads agora autenticados + cota + gestão admin; contradições revisadas),
  [[entities/tcmine-server]] (componentes + endpoints + histórico + frontmatter), `index.md`.
- **Resumo:** confirmado que a **escrita** (`PUT /push`) já era autenticada pelo token Minecraft; as brechas
  eram os **reads abertos** (`GET /manifest`, `POST /bundle`) e a **ausência de cota**. Fechados os reads
  (mesma auth do push; launcher passou a mandar Bearer no pull) e adicionada **cota por conjunto**
  (`PlayerConfigs:MaxSetMb`, default 1 GB). Nova tela `/admin/players` (Owner/Admin) com `MudDataGrid`
  agrupado por jogador para ver/apagar configs, sobre o novo `PlayerConfigAdminService`. Build 0 erro;
  servidor sobe (smoke test).
- **Pendências:** verificação visual da tela pendente (atrás de login). Fail-open ainda reabre os reads
  durante indisponibilidade da Mojang (trade-off aceite, registrado no concept).

## [2026-07-05] ingest | Métricas do sistema globais + card de métricas por instância no dashboard

- **Fonte:** código vivo — implementação a pedido do usuário (TCMine-Server + TCMine-Server.Infrastructure).
- **Páginas afetadas:** [[sources/2026-07-05-global-metrics-per-instance]] (nova),
  [[entities/tcmine-server]] (widgets + histórico + frontmatter),
  [[sources/2026-07-01-dashboard-metrics-home]] (nota de supersessão), `index.md`.
- **Resumo:** o card de sistema do dashboard passou a mostrar métricas **globais do host** — CPU de todos
  os núcleos (`/proc/stat` no Linux, `GetSystemTimes` no Windows) e uso **total do drive** (antes: só o
  processo e só a pasta `tcmine-data`; **substitui** essa parte de 2026-07-01). RAM já era global. Novo
  `ServerInstanceMetricsService` (singleton, lê `GetContainerStatsAsync` do daemon) + novo widget
  `ServerInstancesMetricsCard` que renderiza **um card por instância** (CPU/RAM ao vivo quando rodando;
  ocioso quando parada) — cada card também mostra o **uso em disco** do diretório da instância
  (`SampleDiskAsync`, varredura cacheada 30s, vale rodando ou parada). Build 0/0.
- **Pendências:** verificação visual do dashboard admin pendente (atrás de login; medidores por instância
  exigem containers em execução). Sem página `concepts/` própria — as métricas seguem descritas na entity.

## [2026-07-04] lint | Caça a bugs no fluxo DooD — clamp de RAM da instância (Xms > Xmx não subia)

- **Fonte:** revisão pedida pelo usuário ("veja se existe mais algum bug"). Varredura focada no fluxo de
  provisionamento/instância (onde os últimos bugs apareceram).
- **Achado + fix:** `ServerInstanceService.Apply` gravava `RamMb`/`XmsMb` sem validar. `Xms > Xmx` (ou RAM
  ~0) faz a JVM recusar subir ("Initial heap size larger than maximum"). Adicionados clamps: `RamMb ≥ 512`,
  `XmsMb ∈ [0, RamMb]`, `MaxPlayers ≥ 1`.
- **Revisado e OK:** ciclo de vida do container (`DockerMinecraftManager`: start/stop/reconcile, mounts via
  `ToHostPath`, entrypoint java), container efêmero do instalador (`DockerServerJavaRunner`), instalador do
  loader (`ServerRuntimeInstaller`, com sanidade pós-install), `ServerConfigWriter` (eula=true, jvm args,
  server.properties preservando edições, listas de jogador).
- **Observações (não-fix):** `RestartPolicy=UnlessStopped` faz um servidor que crasha no boot reiniciar em
  loop (reconciler pode oscilar Running/Crashed); e o `-local.N` prerelease do launcher ainda precisa de
  validação real no Velopack. Build 0/0.
- **Páginas afetadas:** só código; log.

---

## [2026-07-04] lint | DooD: linking de libraries/mods via hardlink (symlink quebrava na instância)

- **Fonte:** o usuário provisionou; a pasta da instância foi criada com overrides/mods, mas **sem os links**
  de `libraries/` (e mods) → `Error: could not open 'libraries/net/neoforged/neoforge/21.1.234/unix_args.txt'`.
- **Causa:** produção usava `SymlinkStrategy`, que cria symlinks com **alvo absoluto** do caminho do
  container do TCMine-Server (`/app/tcmine-data/server-cache/…`). O container-irmão da instância (DooD) monta
  só a pasta da instância e **não enxerga** esse caminho → symlink quebrado.
- **Correção:** o `CopyLinkStrategy` passa a **hardlinkar no Linux** (P/Invoke `link(2)` do libc; custo de
  disco ~zero, mesma partição do `tcmine-data`) e vira o **padrão** no `LinkStrategyFactory` (symlink só via
  override, para deploy sem DooD). Hardlink é arquivo real → o container da instância enxerga.
- **Workaround (imagem atual):** `ServerInstances__LinkStrategy=Copy` (copia; funciona já) + re-provisionar.
- **Deploy:** entra no próximo patch. Build 0/0.
- **Páginas afetadas:** só código; log.

---

## [2026-07-04] lint | DooD: DataHostRoot aponta direto para tcmine-data (sem restrição de nome)

- **Fonte:** o usuário (ZimaOS, container pela UI) provisionou e recebeu "bind source path does not exist:
  /app/tcmine-data/server-cache/installed/…" — caminho do container, não do host. A pasta do host era
  `/media/ZimaOS-HD/AppData/tcmine-server` (nome ≠ `tcmine-data`), o que o esquema antigo não permitia.
- **Causa:** `DockerEnvironment.ToHostPath` traduzia relativo à raiz de conteúdo (`/app`), então
  `DataHostRoot` tinha de ser o **pai** de `tcmine-data` E a pasta do host precisava se chamar `tcmine-data`.
- **Correção:** `ToHostPath` passa a ser relativo a `ServerPaths.Data(contentRoot)` (`/app/tcmine-data`), e
  `DataHostRoot` passa a ser o **caminho do host do próprio tcmine-data** (qualquer nome). Default (dev) =
  `ServerPaths.Data(contentRoot)`. `compose.yaml` ajustado (`${PWD}` → `${PWD}/tcmine-data`) e README/tabela.
- **Auto-detecção (dispensa a env var):** a pedido do usuário — em vez de repetir um caminho que já está
  no volume, o `DockerEnvironment` **inspeciona o próprio container** (id via `/proc/self/mountinfo`,
  `InspectContainerAsync`) e lê a origem (host) do mount em `/app/tcmine-data`. Ordem: config explícita →
  auto-detecção → pasta local (dev). `compose.yaml` deixou de setar `DataHostRoot` (só override); README
  atualizado. Assim, no ZimaOS, **nenhuma env de caminho** é necessária — basta o `docker.sock` montado.
- **Deploy:** entra na próxima imagem (patch). Build 0/0.
- **Páginas afetadas:** código + `compose.yaml` + `README.md`; log.

---

## [2026-07-03] ingest | Import puxa descrição + link do CurseForge; badge no launcher/web/dashboard

- **Fonte:** pedido do usuário — no import de modpack, puxar também a descrição e o link do CurseForge, e
  gerar um badge clicável no launcher, na página pública e na dashboard. Decisões: descrição = `summary`;
  badge no admin em **ambas** (lista de Modpacks + Dashboard).
- **Dados:** `ModpackEntity.CurseForgeUrl` (migration `ModpackCurseForgeUrl`, 2 providers). O import busca
  os detalhes do projeto via `CurseForgeApiClient.GetProjectInfoAsync` (novo; lê `summary` + `links.websiteUrl`)
  — feito na infra (`ImportModpackToDraftAsync`), sem tocar no importer puro nem no port. Descrição só
  preenche se vazia; link sempre atualizado. Persistido nos dois saves.
- **Contratos:** `CurseForgeUrl` em `ModpackSummaryDto`, `ModpackManifestDto`, `ModpackAdminRowDto`,
  `DraftImportDto`, `RecentModpack` (+ novo `CfProjectInfoDto`).
- **Badges (cor da marca #F16436):** launcher (Home, `InstalledModpack.CurseForgeUrl` + comando
  `OpenCurseForge`), página pública (`Home.razor`), lista admin (`Modpacks.razor`), **detalhe** do modpack
  (`ModpackHub.razor`) e dashboard (`RecentModpacksCard`).
- **Botão "Compilar launcher" sempre habilitado** (Releases): `_canBuild` deixou de exigir `_needsBuild` —
  o admin pode recompilar para reaplicar uma config alterada (URL/Azure) ao Setup mesmo com o feed na
  última versão. Legenda do estado "atualizado" ajustada.
- **Versionamento de rebuild por config (`AppVersion.BuildVersion`):** ao recompilar por cima de uma
  versão já publicada, gera um prerelease do **próximo** patch — `X.Y.(Z+1)-local.N` (N incrementa) — que
  ordena **entre** a atual e a próxima release do GitHub (`1.0.1 < 1.0.2-local.1 < 1.0.2`). Assim os
  launchers instalados atualizam, e a release `launcher-v1.0.2` cheia depois supera os `-local.*`.
  Correção conceitual: prerelease pendura no **próximo** patch, não no atual (`1.0.1-beta` seria < 1.0.1).
  A verificar num rebuild real: o Velopack servir prereleases no canal `win` (esperado: sim).
- **Nota:** modpacks já existentes só ganham descrição/link ao **re-importar/atualizar** (o import novo
  captura). Build 0/0.
- **Páginas afetadas:** só código; log. (Candidato a doc em [[concepts/modpack-admin-editor]] /
  [[decisions/curseforge-update-tracking]].)

---

## [2026-07-03] lint | Razor renderizava `v@Version` cru (heurística de e-mail do `@`)

- **Fonte:** o usuário viu "v@Version" literal no diálogo de compilar launcher (e o botão "COMPILAR V@VERSION").
- **Causa:** `v@Version` cola o `@` a texto de ambos os lados → o Razor trata como **e-mail** e imprime literal.
- **Correção:** `@(Version)` explícito. Corrigido em `LauncherBuildDialog.razor` (2×) e no banner de update do
  servidor em `Releases.razor` (`v@su.LatestVersion`/`v@su.CurrentVersion` → `@(...)`).
- **Páginas afetadas:** só código; log. Build 0/0.

---

## [2026-07-03] lint | Aviso de update do servidor nunca aparecia (tag com prefixo quebrava o semver)

- **Fonte:** o usuário criou `server-v1.0.1` mas o painel (Releases) não mostrou "atualização disponível".
- **Causa:** `GitHubReleaseService` passava a **tag crua** (`server-v1.0.1`) ao `AppVersion.IsNewer`. O
  `AppVersion.Parse` só tira o `v` inicial e corta no primeiro `-` (lógica de pré-lançamento) → sobra
  `"server"` → `0.0.0` → nunca é "mais novo" que a versão corrente. O banner do servidor **nunca** aparecia.
  (A faixa do launcher não tinha o bug — o auto-build já comparava a versão sem prefixo.)
- **Correção:** comparar a versão **sem** o prefixo (`Strip(tag, "server-v")`) no `ServerTrack`.
- **Botão "Verificar atualizações"** na página Releases: força `GitHub.GetAsync(force: true)` (ignora o cache
  de 6h) via `BusyService`, com snackbar do resultado — para não depender do TTL de 6h.
- **Nota:** cache de 6h + o servidor v1.0.0 em produção **tem o bug** — não vai detectar a 1.0.1. Atualizar
  agora é manual (`docker compose pull && up -d`); a detecção passa a funcionar a partir da versão corrigida.
- **Páginas afetadas:** só código; log. Build 0/0.

---

## [2026-07-03] lint | Launcher fechava no boot: PublicBaseUrl sem esquema → UriFormatException

- **Fonte:** o usuário instalou o launcher em produção e ele fechava logo ao abrir (sem UI). Event Viewer
  (.NET Runtime 1026) deu o stack: `System.UriFormatException` em `ServerConfig.Resolve` ←
  `UpdateService..ctor` ← composição no `Program.BuildAvaloniaApp`.
- **Causa:** a `PublicBaseUrl` configurada no painel estava **sem esquema** (ex.: `host:8080`); embutida no
  launcher, `new Uri(BaseUrl)` lançava e derrubava o app antes da janela (o `UpdateService` é criado eager
  na composição do Splat).
- **Correção imediata (usuário):** corrigir a URL com `http://`/`https://`, recompilar e reinstalar.
- **Blindagem (código):** (1) launcher `ServerConfig` normaliza — sem esquema assume `https`, inválida →
  fallback de dev; `Resolve` nunca mais lança no boot. (2) servidor `LauncherBuildService` bloqueia o build
  se a URL não for absoluta http(s), com mensagem clara. (3) `Settings.razor.cs` valida a URL ao salvar.
- **Páginas afetadas:** só código; log. Build 0/0 (3 avisos pré-existentes no IconGenerator, sem relação).

---

## [2026-07-03] ingest | Ícone + splash no Setup.exe do launcher (vpk pack)

- **Fonte:** o usuário notou que o Setup do launcher saía sem o ícone do projeto.
- **Causa:** o `vpk pack` não passava `--icon` nem `--splashImage` → Setup com o ícone genérico do Velopack.
- **Correção:** `LauncherBuildService` agora adiciona `--icon Assets/icon.ico` e `--splashImage Assets/splash.png`
  (assets já committados em `TCMine-Launcher/Assets/`; resolvidos na fonte baixada via `Path.GetDirectoryName(project)`,
  guardados por `File.Exists`). Infra compila 0/0.
- **Assinatura de código:** discutido, não implementado — o Velopack suporta (`--signParams`/Azure Trusted
  Signing), mas remover o alerta do SmartScreen exige um **certificado de CA real** (self-signed não ajuda).
  Já registrado como lacuna em [[concepts/launcher-build-velopack]].
- **Páginas afetadas:** [[concepts/launcher-build-velopack]] (passo do pack), log.

---

## [2026-07-03] meta | Produção usa a imagem do Docker Hub + fix do DataHostRoot no compose

- **Fonte:** o usuário esclareceu que produção **não** é clonar/buildar (isso é dev), e sim **puxar a
  imagem pública** `jocian/tcmine-server` (buildada pelo GitHub Actions na tag `server-v*`).
- **Compose:** `image: jocian/tcmine-server:latest` (removido o `build:` — produção só puxa),
  `ServerInstances__Image=jocian/tcmine-server:latest`. **Fix:** `ServerInstances__DataHostRoot` corrigido
  de `${PWD}/TCMine-Server` para `${PWD}` (o bind virou `./tcmine-data`, então o pai é `${PWD}`) — sem isso
  as instâncias Minecraft (DooD) montavam caminho vazio. Comentários atualizados.
- **README:** seção de produção reescrita — "Clonar o repositório" virou "Obter o compose.yaml" (curl do
  raw, sem clonar); subir passa a `docker compose up -d` (pull, sem build); manutenção vira
  `docker compose pull && up -d`; adicionada a seção "4. Persistência (volume de dados)" com a regra do
  DataHostRoot; exemplos de versão ajustados p/ 1.0.0.
- **Pendências:** o `compose.yaml` corrigido precisa ser commitado/pushado p/ o `curl` do raw servir a
  versão certa. Falta configurar descrição/overview do repo no Docker Hub (`jocian/tcmine-server`).
- **Páginas afetadas:** só `README.md`, `compose.yaml` (código/docs); log.

- **Fonte:** pedido do usuário — README explicando o projeto, tecnologias, referências à docs e um
  tutorial de produção bem detalhado.
- **Criado:** `README.md` na raiz — sobre/tecnologias/arquitetura (tabela dos 8 projetos), links para a
  wiki (`TCMine-Docs/wiki/index.md`, entities/concepts/decisions/log) e `CLAUDE.md`, e um tutorial passo a
  passo de produção via Docker Compose (`.env` do banco → `docker compose up --build` → `/setup` do Owner →
  segredos no painel → reverse proxy/PublicBaseUrl → publicar launcher). Fatos verificados no código
  (migrations no boot em `Program.cs`, `/setup`, porta 8080, vars de banco).
- **Páginas afetadas:** só `README.md` (referencia a wiki; não altera páginas).
- **Resumo:** documentação; sem mudança de código.

---

## [2026-07-03] meta | Config do banco por vars separadas (DB_HOST/DB_NAME/DB_USER/DB_PASSWORD)

- **Fonte:** o usuário quer configurar o container do TCMine-Server por variáveis de banco separadas
  (provider, database, usuário, senha), em vez da connection string única.
- **Implementado:** `AddTcMineDatabase` passa a resolver a connection string por prioridade —
  `DB_CONNECTION` (completa) → vars separadas `DB_HOST`/`DB_PORT`/`DB_NAME`/`DB_USER`/`DB_PASSWORD` (só
  Postgres, montadas via `NpgsqlConnectionStringBuilder`) → `appsettings` → padrão. `compose.yaml` ganhou
  as vars (via `${...}` de um `.env` não versionado); criado um `.env` local de exemplo.
- **Convenção:** atualizado o `CLAUDE.md` (seção Configuração) e [[decisions/persistence-dual-provider]].
- **Páginas afetadas:** [[decisions/persistence-dual-provider]], `CLAUDE.md`, [[log]].
- **Resumo:** infra compila 0/0. Backward-compat: `DB_CONNECTION` continua ganhando; SQLite ignora as vars.

---

## [2026-07-03] lint | Fix: diálogo de versão de mod renderizava colapsado (sem lista/scroll)

- **Fonte:** o usuário reportou que "mudar versão" de um mod abria um diálogo com pills vazias, sem texto
  e sem scroll, transbordando a tela (print).
- **Causa:** `ModVersionPickerDialog.razor` montava a lista como `MudStack` + `MudButton` por linha; os
  botões colapsavam e o `max-height` no `MudStack` não segurava o scroll.
- **Correção:** reescrito no padrão comprovado do `CurseForgeSearchDialog` — `<div>` com
  `max-height:55vh; overflow-y:auto` + `MudList`/`MudListItem` (clique escolhe a versão). Code-behind
  (`Pick`/`Cancel`) inalterado.
- **Páginas afetadas:** só código (`ModVersionPickerDialog.razor`); sem mudança de wiki além deste log.
- **Resumo:** compila 0/0 (validado em saída temporária — o servidor estava a correr e travava o `bin`).
  Blazor Server: precisa **rebuild + restart** do servidor para aparecer.

---

## [2026-07-03] lint | Drop da tabela órfã `PlayerAccounts`

- **Fonte:** o usuário reportou uma tabela `PlayerAccounts` sem uso no banco.
- **Diagnóstico:** órfã do login brokered pelo servidor (substituído pelo MSAL no launcher — ver
  [[decisions/auth-msal-launcher]]). Zero referências em código (`.cs`); só aparecia em docs. A migration
  `PlayerAccount` que a criava foi apagada do histórico no revert, então nenhum `DropTable` a removia.
- **Correção:** migration `DropOrphanPlayerAccounts` (Sqlite + Postgres) com
  `DROP TABLE IF EXISTS "PlayerAccounts"` idempotente (remove onde sobrou; no-op em bancos limpos).
- **Páginas afetadas:** [[decisions/auth-msal-launcher]] (nota de limpeza posterior), [[log]].
- **Resumo:** build limpo (0/0). Migration **não aplicada** — `dotnet ef database update` fica a critério
  do usuário (o boot do Docker aplica sozinho).

---

## [2026-07-03] ingest | Sync de configs: feedback de status na label da Home

- **Fonte:** pedido do usuário (mesma sessão). A label de status da Home passa a informar quando o launcher
  faz download/upload das configs.
- **Implementado:** `PlayerConfigSync.Pull/PushAsync` ganham um `Action<string>? report`; o `LaunchOrchestrator`
  liga o pull ao progresso do prepare e o `ILaunchOrchestrator.PushConfigsAsync` recebe o report. A shell
  (`MainWindowViewModel.MonitorGameAsync`) mostra o status do push na `LaunchStatus` (marshalado p/ UI thread).
  Mensagens: "A baixar/enviar configurações do jogador (N ficheiros)…".
- **Páginas afetadas:** [[concepts/player-config-sync]] (bullet de feedback na UI).
- **Resumo:** build limpo (0/0).

---

## [2026-07-03] decisao | Sync de configs: diff incremental + cache de mapa só de servidor

- **Fonte:** direção do usuário (mesma sessão). Dois pedidos: (1) **não** enviar o zip inteiro — só o
  **diff**, para não sobrecarregar a rede; (2) no cache de mapa, incluir só o **mundo do servidor**
  atrelado ao modpack, **não** os mundos singleplayer locais que o jogador cria.
- **Protocolo (novo):** ficheiros **descompactados em disco** no servidor + `.tcmine-manifest.json`
  (caminho→SHA-256+tamanho). Rotas `GET /manifest`, `POST /bundle` (baixa só o que falta), `PUT /push`
  (envia só o alterado + manifesto; servidor reconcilia remoções). Substitui o par GET/PUT de zip único.
  Contrato partilhado em `TCMine-Application/Contracts/PlayerConfig.cs`.
- **Allowlist (`PlayerDataProfile`) estreitado:** `config/xaero*`, `journeymap/config`,
  `XaeroWaypoints/Multiplayer*`, `XaeroWorldMap/Multiplayer*`, `journeymap/data/mp` — só multiplayer;
  `data/sp`/`Singleplayer_*` ficam de fora. (Também muda o snapshot/restore de overrides, que usa o mesmo
  profile.)
- **Páginas afetadas:** [[concepts/player-config-sync]] (reescrita das seções de protocolo e allowlist),
  [[sources/2026-07-03-player-config-sync-completo]].
- **Resumo:** build limpo (0/0). Pull não apaga ficheiros locais; remoções propagam via push.
- **Pendências:** hashing a cada sync (otimizável com cache size+mtime); custo de disco do cache de mapa.

---

## [2026-07-03] ingest | Sync de configs do jogador completado fim-a-fim (storage em disco)

- **Fonte:** código vivo (implementação nesta sessão). Pedido do usuário: salvar as configs do jogador
  (começando pelas teclas) no servidor, para não se perderem ao atualizar o modpack ou trocar de PC.
- **Decisões do usuário (sessão):** escopo = **reusar o `PlayerDataProfile`** (options.txt + shaders +
  minimapa Xaero/JourneyMap); trigger = **automático** (pull no prepare, push ao fechar o jogo);
  storage = **disco** (não BD) e **manter o cache do mapa** (JourneyMap inteiro).
- **Estado anterior:** só esqueleto — `PlayerConfigEntity` sem blob (o zip era descartado), só `PUT`, e
  launcher sem código de sync.
- **Pivot:** a 1ª versão guardava o zip como **blob em BD** (migration `PlayerConfigBlob`). Como o usuário
  quis disco + cache de mapa (100s de MB), reverteram-se as migrations de Blob, removeu-se toda a camada
  EF do player-config e migrou-se para **ficheiros em disco com streaming**.
- **Implementado (final):** servidor — endpoint faz storage em `tcmine-data/player-configs/{uuid}/{id}.zip`
  (`ServerPaths.PlayerConfigs`), `GET`/`PUT` por streaming, teto 256 MB (`413` + limite de corpo do Kestrel
  levantado por pedido), header `X-Config-Updated` (mtime); migration `DropPlayerConfigs` (SQLite+Postgres)
  largou a tabela. Launcher — `PlayerConfigSync` (pull/push via ficheiro temporário), `ConfigSyncedAt` no
  `InstalledModpack` (last-write-wins), wiring no `LaunchOrchestrator` + `ILaunchOrchestrator.PushConfigsAsync`
  + `MainWindowViewModel.Play.cs`.
- **Páginas afetadas:** [[concepts/player-config-sync]] (reescrita → `stable`),
  [[concepts/launcher-install-launch]] (contradição resolvida),
  [[sources/2026-07-03-player-config-sync-completo]], [[index]]; correções cruzadas em
  [[entities/tcmine-application]], [[entities/tcmine-server-infrastructure]], [[concepts/clean-architecture]]
  (removida a menção a `IPlayerConfigRepository`).
- **Resumo:** build limpo (0 erros/avisos); migration `DropPlayerConfigs` gerada nos dois providers e
  verificada (`DropTable`).
- **Pendências:** sem merge de conflitos (last-write-wins); vigiar tamanho de `player-configs/` com o cache
  de mapa. Migration `DropPlayerConfigs` **não aplicada** ao banco de dev (`dotnet ef database update` fica
  a critério do usuário).

---

## [2026-07-02] decisao | Reverte p/ DUAS versões (server-v*/launcher-v*) + fonte do launcher baixada

- **Fonte:** o usuário identificou a sobrecarga do modelo de versão única (mudar só o launcher forçava
  rebuild+restart da imagem; mudar só o server gerava update falso pros players). Escopo confirmado:
  **baixar a fonte do launcher do GitHub sob demanda**. Arquivos: `GitHubReleaseService.cs` (duas faixas),
  `LauncherBuildService.cs` (fetch+extract da tag, dirigido por `launcher-v*`), `LauncherAutoBuildService.cs`
  (poll 1h), `Dockerfile` (sem fonte embutida), `.github/workflows/server-image.yml` (tag `server-v*`),
  `Releases.razor(.cs)` + `LauncherBuildDialog`.
- **Páginas afetadas:** [[concepts/launcher-build-velopack]] (seção de versionamento reescrita), [[index]].
- **Resumo:** volta ao modelo de **duas faixas** do backup — `server-v*` (imagem) e `launcher-v*` (código
  do launcher), independentes. O servidor **em execução** baixa a fonte do launcher na tag `launcher-v*`
  (tarball do GitHub, extraído com `System.Formats.Tar`), compila e publica o feed — **sem rebuild de
  imagem nem restart**. A imagem Docker ficou **leve** (SDK+vpk+JRE, sem fonte embutida). `GitHubReleaseService`
  agora devolve duas faixas; a página mostra o banner de update do server (server-v*) e o estado do launcher
  (última launcher-v* vs feed). Auto-build no boot/settings/**poll 1h**. Server compila 0/0, boot limpo.
- **Pendências:** validar o fetch+build ao vivo (precisa de uma tag `launcher-v*` real no repo público);
  secrets DOCKERHUB no GitHub; assinatura de código.

## [2026-07-02] ingest | Consumidor de auto-update no launcher (Velopack UpdateManager)

- **Fonte:** continuação (o usuário pediu "sim pode fazer"). Arquivos: `TCMine-Application/Launcher/IUpdateService.cs`
  (novo), `TCMine-Launcher.Infrastructure/UpdateService.cs` (novo) + `.csproj` (Velopack),
  `TCMine-Launcher/ViewModels/MainWindowViewModel.cs`, `Views/MainWindow.axaml`, `Program.cs`.
- **Páginas afetadas:** [[concepts/launcher-build-velopack]], [[entities/tcmine-launcher]], [[index]].
- **Resumo:** porta `IUpdateService` + impl Velopack (`UpdateManager` contra `{servidor}/updates`, canal
  `win`). O shell checa no boot (guardando `IsInstalled` — dev não checa) e mostra um **banner de update**
  com "Atualizar agora" → baixa (progresso %), aplica e reinicia. Fecha o ciclo do
  [[concepts/launcher-build-velopack]]. Solução compila 0/0.
- **Pendências:** validar ao vivo (precisa de uma app instalada via Setup.exe + 2 versões no feed);
  assinatura de código (SmartScreen).

## [2026-07-02] decisao | Versionamento único (server=launcher) + self-update + auto-build

- **Fonte:** design com o usuário (repo `tiny-core/TCMine`; manter imagem autossuficiente). Escolhas:
  **uma versão só** e **auto-recompilar no boot** (pega carona no restart do container). Arquivos:
  `.github/workflows/server-image.yml` (novo), `TCMine-Server/Dockerfile` (ARG/ENV SERVER_VERSION),
  `AppVersion.cs`, `GitHubReleaseService.cs`, `LauncherAutoBuildService.cs` (novos),
  `LauncherBuildService.cs`, `Releases.razor(.cs)` + `LauncherBuildDialog`, `Program.cs`.
- **Páginas afetadas:** [[concepts/launcher-build-velopack]], [[index]].
- **Resumo:** modelo de **uma versão** — tag `v*` → Actions builda a imagem (`SERVER_VERSION`) → Docker Hub;
  o server lê a própria versão (`AppVersion`) e o launcher é compilado **nessa** versão. `GitHubReleaseService`
  (cache 1h) compara com as releases `v*` do GitHub → **banner de update do servidor**.
  `LauncherBuildService` ganhou `TargetVersion`/`NeedsBuild`/`TryStartAutoAsync`; o botão compila na versão
  do server e **desabilita quando o feed já está nela**. `LauncherAutoBuildService` (hosted) recompila no
  **boot** e ao **salvar settings** se desatualizado + settings prontas. Server compila 0/0, boot limpo.
- **Pendências:** consumidor no launcher (`UpdateManager` contra `/updates`); testar o auto-build/self-update
  ao vivo (precisa da imagem no Docker Hub + settings); DOCKERHUB secrets no GitHub.

## [2026-07-01] ingest | Launcher build: guarda de settings + cross-compile Linux→Win + imagem autossuficiente

- **Fonte:** feedback do usuário (bloquear sem settings; a imagem Docker deve levar o SDK e compilar o
  launcher no Linux, autossuficiente). Arquivos: `LauncherBuildService.cs`, `Releases.razor(.cs)`,
  `TCMine-Server/Dockerfile`, `.dockerignore`, `concepts/launcher-build-velopack.md`.
- **Páginas afetadas:** [[concepts/launcher-build-velopack]], [[index]].
- **Resumo:** (1) **guarda**: compilar exige URL pública **e** Azure Client Id — botão desabilitado + aviso,
  e o serviço recusa. (2) **Cross-compile validado**: testei num container `sdk:10.0` que o `vpk` gera o
  feed de **Windows a partir do Linux** com o diretório **`[win]`** (`vpk [win] pack`) — gerou `Setup.exe`
  (58 MB), `nupkg`, `RELEASES`. O serviço adiciona `[win]` quando `!IsWindows()`. (3) **Dockerfile
  autossuficiente**: base SDK + fonte do launcher + vpk + JRE + `restore -r win-x64`, com
  `LauncherBuild__ProjectPath` apontando para `/src`. Server compila 0/0.
- **Pendências (o fluxo completo pedido pelo usuário, ainda a fazer):** GitHub Actions `server-v*` →
  DockerHub (mirror do bk); `SERVER_VERSION` embutido; `GitHubReleaseService` (aviso de update do server +
  versão-alvo do launcher); compilar o launcher **na versão do release** e **desabilitar o botão** quando o
  feed já está nessa versão.

## [2026-07-01] ingest | Compilação do launcher pelo servidor (Velopack)

- **Fonte:** implementação a pedido. Escopo confirmado: página dedicada `/admin/releases` + diálogo de
  versão/notas. Arquivos: [[sources/2026-07-01-launcher-build-velopack]].
- **Páginas afetadas:** [[concepts/launcher-build-velopack]] (nova), [[entities/tcmine-server]],
  [[entities/tcmine-server-infrastructure]], [[entities/tcmine-launcher]], [[index]].
- **Resumo:** novo `LauncherBuildService` (singleton, job de fundo reconectável) faz `dotnet publish` do
  launcher (injetando `TcmineServerUrl`/`MicrosoftClientId`) + `vpk pack` → feed Velopack em
  `tcmine-data/updates`, e grava `ReleaseEntity`. Página `/admin/releases` (Owner/Admin) com estado do
  feed, botão compilar, progresso ao vivo e histórico. **Pré-requisito descoberto e resolvido:** o launcher
  precisava de `VelopackApp.Build().Run()` no `Main` (pacote `Velopack`) — sem isso o `vpk pack` recusa.
  **Validado ponta-a-ponta** (publish + pack geram `RELEASES`/`-full.nupkg`/`-Setup.exe`). Solução 0/0.
- **Pendências:** consumidor no launcher (`UpdateManager` contra `/updates` + "atualizar" na UI);
  assinatura de código; build em Docker/Linux runtime-only não suportado.

## [2026-07-01] meta | Rename: TCMine-Infrastructure → TCMine-Server.Infrastructure

- **Fonte:** pedido do usuário. Ele pensou em **mesclar** as duas infras (server + launcher) num só lugar;
  após ver o trade-off (mesclar arrastaria EF/Docker pro launcher e CmlLib pro server, revertendo
  [[decisions/launcher-clean-architecture]]), optou por **renomear** a infra do server para ficar
  **simétrica** com a do launcher, mantendo o isolamento.
- **Páginas afetadas:** [[entities/tcmine-server-infrastructure]] (renomeada de `tcmine-infrastructure`),
  [[decisions/launcher-clean-architecture]] (nota do rename), [[index]], `CLAUDE.md`, e todos os wikilinks
  que apontavam para a página antiga.
- **Resumo:** projeto/pasta/assembly `TCMine-Infrastructure` → `TCMine-Server.Infrastructure`; namespace
  `TCMine_Infrastructure` → `TCMine_Server.Infrastructure` (100 arquivos .cs/.razor, 148 ocorrências);
  `RootNamespace`/`Title`/`Description` do csproj, `ProjectReference` do `TCMine-Server`, `TCMine.slnx` e
  comentários atualizados. Sem `MigrationsAssembly` fixado → as migrations continuam válidas (IDs na
  tabela de histórico inalterados). **Solução compila 0/0** (server e slnx). Nada mesclado — o isolamento
  server↔launcher permanece; o compartilhado real (models/contratos) já vive em Domain/Application.
- **Pendências:** `publish/*.deps.json` tem o nome antigo (artefato de build; regenera no próximo publish).

## [2026-07-01] decisao | Provisionamento durável e reconectável (ProvisioningCoordinator)

- **Fonte:** pedido do usuário (recuperar a conexão ao container se o server parar ou a página der refresh).
  Escopo confirmado: **completa (reconectar progresso)**. Arquivos:
  `TCMine-Infrastructure/ServerInstances/{ProvisioningCoordinator(novo),DockerServerJavaRunner,ServerRuntimeInstaller}.cs`,
  `TCMine-Application/Abstractions/IServerJavaRunner.cs`, `TCMine-Domain/Entities/ServerInstanceEntity.cs`,
  `TCMine-Server/Program.cs`, `.../Servers/ServerInstanceDetail.razor(.cs)`, `.../Servers/ServerInstances.razor.cs`,
  `.../Modpacks/ModpackHub.razor.cs`.
- **Páginas afetadas:** [[concepts/server-instance-lifecycle]], [[concepts/async-feedback-overlay]], [[index]].
- **Resumo:** a provisão saiu do circuito Blazor (onde refresh a matava e o DbContext scoped era descartado)
  para um **`ProvisioningCoordinator`** singleton — jobs em tarefa de fundo com escopo de DI próprio,
  progresso em memória + evento `Changed`. A página **reconecta** ao progresso após refresh (inscreve-se no
  coordenador; painel de passos próprio, não mais o `BusyOverlay`). Container do instalador com **nome
  determinístico** `tcmine-install-{slug}` + runner **remove órfão de mesmo nome** antes de criar. Novo
  estado `ServerInstanceStatus.Provisioning` (persistido, coluna string → sem migration); no boot,
  `RecoverAsync()` **retoma** provisões interrompidas (re-provisão limpa/idempotente); falha volta a
  `Stopped`. Server compila 0/0; boot limpo (coordenador registrado, RecoverAsync sem erro).
- **Pendências:** **não testado ao vivo** (login + Docker + provisão real + refresh). Decisão de projeto:
  no restart, a provisão é **re-executada limpa** (não reatacha o container em execução) — mais simples e
  robusto que reatachar a um estado semi-escrito; um install em andamento no restart é refeito do zero.

## [2026-07-01] ingest | Instalador NeoForge "parece travado": heartbeat + timeout

- **Fonte:** usuário reportou container preso (log parado em "Splitting 12951 files"). Arquivos:
  `TCMine-Infrastructure/ServerInstances/{ServerRuntimeInstaller,DockerServerJavaRunner}.cs`.
- **Páginas afetadas:** [[concepts/server-instance-lifecycle]], [[index]].
- **Resumo:** o `--installServer` do NeoForge tem fases longas e **silenciosas** (RENAME/ART após o split
  processa milhares de classes sem imprimir) — não estava travado, só sem feedback, e o overlay congelava
  na última linha. Adicionado **heartbeat de 2s** no `ServerRuntimeInstaller` que publica a última linha +
  **tempo decorrido (mm:ss)** (sobe mesmo nas fases mudas). E um **timeout de 30 min** no
  `DockerServerJavaRunner` (CTS ligado ao ct) como rede de segurança: se travar de verdade, remove o
  container e lança `TimeoutException` clara. Recuperação de install interrompido é automática (a linha de
  cache só é gravada no fim; a próxima provisão apaga o installDir e reinstala limpo). Server compila 0/0.
- **Pendências:** se o hang for real e recorrente, provável causa = recursos do Docker (CPU/memória) —
  não instrumentado; o timeout apenas mitiga.

## [2026-07-01] ingest | Formatação do BusyOverlay: status global + lista com scroll

- **Fonte:** pedido do usuário (formatar a tela de provisionar). Arquivos:
  `TCMine-Server/Components/Shared/BusyOverlay.razor(.cs/.css)`, `TCMine-Server/wwwroot/js/server-console.js`.
- **Páginas afetadas:** [[concepts/async-feedback-overlay]], [[index]].
- **Resumo:** o overlay ganhou hierarquia: spinner → **status global** (a fase atual limpa, derivada do
  rótulo antes de `" — "` via `GlobalStatus`; o detalhe técnico do instalador some do título e fica só na
  lista) → **lista de passos** com altura limitada (`max-height:220px` + scroll) e **auto-scroll** para o
  passo atual (`tcmineScrollToBottom` + `ElementReference`). Estilo em `BusyOverlay.razor.css` (elementos
  HTML próprios); larguras do `MudPaper`/`MudText` por inline (CSS escopado não pega componentes filhos).
  Server compila 0/0; home renderiza sem erros (overlay em si só aparece em operação, atrás de login).
- **Pendências:** verificação visual do overlay ao vivo (provisão real com Docker) não feita nesta sessão.

## [2026-07-01] lint | Fix: crash de captura de variável de laço no BusyOverlay

- **Fonte:** `ArgumentOutOfRangeException` no circuito ao renderizar o log de passos. Arquivo:
  `TCMine-Server/Components/Shared/BusyOverlay.razor`.
- **Páginas afetadas:** [[concepts/async-feedback-overlay]] (nenhuma mudança de conteúdo), [[index]] (n/a).
- **Resumo:** o `@for (var i …)` do log de passos usava `@Busy.Steps[i]` dentro do `ChildContent` do
  `MudText` — um `RenderFragment` **adiado** que captura `i` por referência; ao executar após o fim do
  laço, `i == Steps.Count` → índice fora do intervalo. Corrigido capturando `var step = steps[i]` (valor
  por iteração) e usando `@step`. Confirma, de quebra, que o fix do `Task.Run` funcionou: o overlay
  **estava** renderizando a lista de passos (só quebrava neste ponto). Server compila 0/0.
- **Pendências:** nenhuma.

## [2026-07-01] decisao | Progresso do provisionamento não renderizava (dispatcher preso) → Task.Run

- **Fonte:** bug reportado (overlay fica em "Provisionando instância…" e fecha sem mostrar os passos).
  Arquivo: `TCMine-Server/Components/Pages/Admin/Servers/ServerInstanceDetail.razor.cs`.
- **Páginas afetadas:** [[concepts/async-feedback-overlay]], [[concepts/server-instance-lifecycle]], [[index]].
- **Resumo:** o provisionamento tem trabalho **síncrono pesado** (link de arquivos) que, rodando no
  dispatcher do circuito Blazor, **engolia o progresso** — os `progress.Report` só renderizavam depois da
  operação terminar (quando o overlay já limpou os passos). Correção: `RunAndReload` roda a operação em
  **`Task.Run`** (libera o dispatcher) e o `Progress` é criado **no circuito** em `ProvisionAsync` (para os
  callbacks marshalarem de volta ao dispatcher, agora livre para renderizar cada passo). Acesso ao
  DbContext é sequencial (overlay bloqueia interação; loops de ping/log não tocam o db), então rodar noutra
  thread é seguro. Server compila 0/0.
- **Pendências:** confirmar com o usuário que os passos aparecem agora; se ainda falhar rápido, o snackbar
  de erro passa a ser visível *depois* do último passo renderizado (diagnóstico).

## [2026-07-01] ingest | Streaming ao vivo do instalador NeoForge no provisionamento

- **Fonte:** pedido do usuário (dar sequência à pendência do item anterior). Arquivos:
  `TCMine-Application/Abstractions/IServerJavaRunner.cs`,
  `TCMine-Infrastructure/ServerInstances/{DockerServerJavaRunner,ServerRuntimeInstaller}.cs`.
- **Páginas afetadas:** [[concepts/server-instance-lifecycle]], [[index]].
- **Resumo:** resolve a pendência do log anterior (o passo `java --installServer` não dava progresso ao
  vivo). `IServerJavaRunner.RunAsync` ganhou `IProgress<string>? output`; o `DockerServerJavaRunner` passou
  a seguir os logs com `Follow=true` e ler o **stream multiplexado linha a linha** (acumulando a saída
  completa para o `JavaRunResult`). O `ServerRuntimeInstaller` liga essa saída ao overlay como uma linha
  ao vivo coalescida, com throttle de 150ms e encurtamento. Server compila 0/0.
- **Pendências:** verificação visual (login + Docker + provisão real) não feita; decode UTF-8 por bloco
  pode partir um char multibyte (saída do instalador é ~ASCII — risco desprezível).

## [2026-07-01] ingest | Provisionamento: log de passos + progresso de download no overlay

- **Fonte:** pedido do usuário (mais informação na tela de provisionar). Arquivos:
  `TCMine-Server/Services/BusyService.cs`, `TCMine-Server/Components/Shared/BusyOverlay.razor`,
  `TCMine-Infrastructure/ServerInstances/{ServerRuntimeInstaller,ServerProvisioner}.cs`.
- **Páginas afetadas:** [[concepts/server-instance-lifecycle]], [[concepts/async-feedback-overlay]], [[index]].
- **Resumo:** o `BusyOverlay` mostrava só a última linha de progresso. Agora o `BusyService` acumula um
  **log de passos** (com coalescência: mensagens com o mesmo rótulo antes de `" — "` substituem a linha,
  para o % de download não inundar) e o overlay renderiza a lista (concluídos com check, o atual com
  spinner). O `ServerRuntimeInstaller` ganhou **progresso de bytes** no download do instalador NeoForge
  (`GetAsync` + `ResponseHeadersRead` + throttle 250ms) e mensagens de cache-hit/instalação mais claras;
  o `ServerProvisioner` reporta contexto inicial (loader/MC) e o link de mods coalesce (`n/total`).
  Server compila 0/0.
- **Pendências:** verificação visual do fluxo (atrás de login + Docker) não feita; a instalação do
  NeoForge (`java --installServer`, que baixa MC+libraries no container) não transmite progresso ao vivo —
  o `IServerJavaRunner` devolve o resultado só no fim (streaming seria um passo à parte).

## [2026-07-01] ingest | Disco da dashboard = só os dados do projeto (tcmine-data)

- **Fonte:** pedido do usuário. Arquivos: `TCMine-Infrastructure/Server/SystemMetricsService.cs`,
  `TCMine-Infrastructure/FileSystem/ServerPaths.cs` (novo helper `Data(root)`),
  `TCMine-Server/Components/Pages/Admin/Dashboard/Widgets/SystemStatusCard.razor`.
- **Páginas afetadas:** [[entities/tcmine-server]], [[sources/2026-07-01-dashboard-metrics-home]], [[index]].
- **Resumo:** o medidor de disco deixou de reportar o **drive inteiro** (usado = total − livre) e passou a
  medir **só o tamanho de `tcmine-data`** (varredura recursiva iterativa, pulando reparse points para não
  inflar via os links do server-cache), sobre a **capacidade do drive** como total. A varredura é
  **cacheada (30s)** porque o `Capture()` roda a cada 2s. Gauge renomeado para **"Dados"**, legenda em
  unidade adaptativa (MB/GB). Server compila 0/0.
- **Pendências:** hardlinks (se usados no cache) contam por link (leve sobre-contagem) — aceitável para o
  gauge.

## [2026-07-01] ingest | Launcher: badges de indisponibilidade + servidores via SSE

- **Fonte:** pedido do usuário. Arquivos: `TCMine-Domain/Launcher/InstalledModpack.cs`,
  `TCMine-Launcher/ViewModels/MainWindowViewModel.cs` + `.Play.cs`,
  `TCMine-Launcher/Views/{HomePageView,InstancesPageView}.axaml`,
  `TCMine-Launcher/Themes/Styles/Cards.axaml`.
- **Páginas afetadas:** [[entities/tcmine-launcher]], [[concepts/sse-content-sync]], [[index]].
- **Resumo:** o launcher passou a **sinalizar com badge** quando o modpack de uma instância foi
  removido/despublicado no servidor, ou quando o servidor de auto-join sumiu. Disparado pelo **SSE**
  (`OnServerContentChanged` → `RefreshActiveAsync` + novo `ReconcileAvailabilityAsync`, que cruza as
  instâncias com `/api/modpacks`). O manifesto do ativo agora distingue **404 (removido)** de **offline**.
  `InstalledModpack` ganhou `INotifyPropertyChanged` só para flags de runtime não persistidas
  (`ModpackMissing`/`AutoJoinServerMissing`/`HasAvailabilityWarning`/`AvailabilityMessage`); badges na
  Home (hero) e na lista de Instâncias (`Border.badge.warn`). A lista de servidores do ativo já
  reconstrói via SSE. Launcher compila 0/0.
- **Pendências:** verificação visual do desktop (Avalonia) não feita nesta sessão; instâncias
  **não-ativas** usam a lista de servidores da última visita (o badge de "servidor sumido" do ativo é
  sempre fresco via manifesto).

## [2026-07-01] decisao | Guarda clara ao apagar modpack com servidores atrelados

- **Fonte:** bug reportado pelo usuário (erro genérico ao apagar modpack). Arquivos:
  `TCMine-Infrastructure/Minecraft/ModpackImportService.cs` (`DeleteAsync`),
  `TCMine-Server/Components/Pages/Admin/Modpacks/Modpacks.razor.cs`.
- **Páginas afetadas:** [[entities/tcmine-server]], [[concepts/server-instance-lifecycle]], [[index]].
- **Resumo:** a FK `ServerInstanceEntity → Modpack` é `Restrict` **de propósito**
  (`AppDbContext`, ~L139) — não deixa apagar um modpack com instâncias derivadas. Antes o
  `SaveChanges` falhava com um `DbUpdateException` genérico (só dava para diagnosticar pelo log).
  Agora `DeleteAsync` faz uma **checagem prévia**: se há instâncias com aquele `ModpackId`, lança
  `InvalidOperationException` **nomeando os servidores** e orientando a apagá-los primeiro. A UI
  (`Modpacks.razor.cs`) trata `InvalidOperationException` como **Warning** (regra de negócio) e o
  resto como Error. **Mantido** o `Restrict` (mais seguro que cascatear a remoção de servidores).
- **Pendências:** nenhuma.

## [2026-07-01] ingest | Dashboard com medidores de recurso + home pública revampada

- **Fonte:** código vivo (implementação a pedido). Arquivos:
  `TCMine-Infrastructure/Server/SystemMetricsService.cs`, `TCMine-Server/Program.cs`,
  `.../Dashboard/Widgets/{MetricGauge,SystemStatusCard,ModDistributionCard}.*`,
  `Components/Pages/Home.*`.
- **Páginas afetadas:** [[entities/tcmine-server]] (estado + frontmatter),
  [[sources/2026-07-01-dashboard-metrics-home]] (nova), [[index]].
- **Resumo:** `SystemMetricsService` passou a medir **CPU/RAM/disco** do host/contêiner
  (cross-platform: `TotalProcessorTime`, `GC.GetGCMemoryInfo`, `DriveInfo`; stateful, recebe
  `dataRoot`). `SystemStatusCard` ganhou 3 **medidores circulares** via novo `MetricGauge`;
  `ModDistributionCard` virou **donut** (API MudBlazor 9 `ChartSeries`+`ChartLabels`). **Home
  pública** conectada ao `ContentCatalog` (modpacks publicados + launcher) com hero, destaques
  e grade de modpacks. Server compila 0/0; home validada no browser.
- **Pendências:** verificação visual da **dashboard admin** (atrás de login) não feita;
  possível revisão do rótulo "CPU" (é uso do processo do servidor, não CPU global do host).

## [2026-06-29] decisao | Volta ao MSAL no launcher (revert do server-brokered)

- **Fonte:** decisão do usuário ("voltar para MSAL", sem hosting/redirect-web/secret); backup
  `P:\TCMine-Launcher-bk` como referência. Código novo: `TCMine-Launcher/Services/{AuthService,AppConfig}.cs`;
  removidos no servidor: `AuthEndpoints`, `LoginSessionBroker`, `MicrosoftAuthService`, `PlayerSessionService`,
  `PlayerAccountEntity`(+repo/porta), campo client secret e migrações `PlayerAccount`/`AzureClientSecret`.
- **Páginas afetadas:** [[decisions/auth-msal-launcher]] (nova, aceita), [[decisions/server-brokered-microsoft-login]]
  (→ **substituída**), [[entities/tcmine-launcher]] (auth via MSAL), [[entities/tcmine-server]] (sem endpoints de
  auth), [[index]].
- **Resumo:** o fluxo server-brokered exigia redirect **Web** no Azure (acoplado à hospedagem, indefinida)
  e **client secret**. O usuário preferiu o modelo do backup: **MSAL no launcher** (CmlLib + XboxAuthNet,
  public client, redirect **loopback**, cache DPAPI) — sem hosting, sem redirect web, sem secret. **Revert
  limpo** de todo o lado servidor; `MinecraftAuthService` (validação para sync de configs) permanece.
  Snapshots do EF restaurados via git; `has-pending-model-changes` = limpo. Servidor e launcher compilam 0/0.
- **Pendências:** o app Azure precisa de "Mobile and desktop applications" com redirect `http://localhost` +
  account type com contas pessoais (`signInAudience`) + `MicrosoftClientId` no build (Client.props/-p). Se o
  servidor chegou a aplicar a migração `PlayerAccount` durante os testes, sobra uma tabela `PlayerAccounts`
  órfã no Postgres (inofensiva; pode ser dropada). Install/launch do jogo continua fora de escopo.

---

## [2026-06-29] decisao | Login Microsoft vira cliente confidencial (client secret)

- **Fonte:** erro real do usuário no login (AADSTS650053) + ele não ter hosting definido; código
  `TCMine-Domain/Entities/ServerSettingEntity.cs`, `TCMine-Infrastructure/Server/ServerSettingsService.cs`,
  `TCMine-Infrastructure/Minecraft/MicrosoftAuthService.cs`, `TCMine-Server/Components/Pages/Admin/Settings.razor(.cs)`,
  migração `AzureClientSecret` (Sqlite+Postgres).
- **Páginas afetadas:** [[decisions/server-brokered-microsoft-login]] (PKCE-público → confidencial+secret;
  seção de setup Azure), [[entities/tcmine-server]] (settings ganham client secret).
- **Resumo:** o login server-brokered é um **web-app confidencial** (o servidor troca o code server-side),
  então passou a usar **client secret** guardado cifrado (Data Protection), ao lado do client/tenant id já
  existentes. Novo campo no `/admin/settings`. PKCE mantido como defesa extra. **Diagnóstico do
  AADSTS650053**: o app Azure precisa suportar contas pessoais (`signInAudience` com
  PersonalMicrosoftAccount) e tenant `consumers` (não GUID de org) — senão o scope `XboxLive.signin` cai
  no Graph. **Hosting não é bloqueio**: registrar o redirect Web `https://localhost:7002/auth/microsoft/callback`
  para dev e adicionar produção depois (o MSAL antigo não exigia isso por ser public-client com loopback).
- **Pendências:** validar o login fim-a-fim após o usuário ajustar o app no Azure (account type + secret +
  redirect) e reiniciar o servidor (a migração aplica no boot).

---

## [2026-06-29] ingest | Novidades no launcher (globais + de modpacks) + endpoint /api/news

- **Fonte:** pedido do usuário. Servidor: `Contracts/Server.cs` (`NewsItemDto`), `ModpackNewsService.ListPublishedAsync`,
  `Endpoints/NewsEndpoints.cs` (+ `Program.cs`). Launcher: porta `INewsFeed`, impl `NewsFeed`,
  `NewsPageViewModel`/`NewsPageView`.
- **Páginas afetadas:** [[entities/tcmine-server]] (`/api/news`), [[entities/tcmine-launcher]] (aba Novidades),
  [[concepts/sse-content-sync]] (news também recarrega via SSE).
- **Resumo:** o servidor já guardava novidades (`NewsEntity`, `ModpackId` null=global) e fazia bump SSE
  nas mutações, mas **não expunha feed público**. Adicionado `GET /api/news` (só publicadas, globais + de
  modpacks, com nome do modpack). O launcher ganhou a **aba Novidades**: feed unificado com badge de
  origem (nome do modpack ou "GLOBAL") + tag + título + resumo + data, recarregado ao vivo pelo SSE.
  Removido o código morto de placeholder (todas as páginas agora são reais). Compila 0/0 (servidor +
  launcher).
- **Pendências:** filtro por origem (todas/globais/modpack) não implementado (feed unificado com badges
  cobre o pedido); reinício do servidor necessário para o endpoint entrar.

---

## [2026-06-29] ingest | Launcher: botão JOGAR → ATUALIZAR via SSE

- **Fonte:** pedido do usuário. Código `MainWindowViewModel.Play.cs` (versão instalada vs. mais recente).
- **Páginas afetadas:** [[concepts/launcher-install-launch]], [[concepts/sse-content-sync]].
- **Resumo:** o launcher passou a guardar a **versão mais recente** de cada modpack (do manifesto, viva
  via SSE) **separada** da versão instalada (`InstalledModpack.ManifestVersion`). O refresh/SSE deixou de
  sobrescrever a versão instalada — só os metadados de exibição. `HasActiveUpdate` compara as duas; o
  botão da Home (e o label do card) vira **ATUALIZAR** quando diferem. Clicar reinstala (overrides
  reaplicam) e, no sucesso, a versão instalada = a do servidor → volta a "JOGAR". Compila 0/0.
- **Pendências:** nenhuma.

---

## [2026-06-29] ingest | Launcher: live-link SSE (auto-update ao editar no servidor)

- **Fonte:** feedback do usuário ("não atualiza automaticamente quando edito no servidor"). Nova porta
  `TCMine-Application/Launcher/IContentWatcher`, impl `TCMine-Launcher.Infrastructure/ContentWatcher`,
  ligação no `MainWindowViewModel`; composição no `Program.cs`.
- **Páginas afetadas:** [[concepts/sse-content-sync]] (consumidor no launcher), [[entities/tcmine-launcher]].
- **Resumo:** o **servidor já fazia `ContentNotifier.Bump()`** em todas as mutações (modpack, instâncias,
  news) — o problema era só o launcher novo **não consumir o `/events`**. Adicionado o consumidor SSE:
  fixa a baseline, dispara ao receber versão diferente (em stream **ou após reconectar**), reconecta com
  backoff; o shell **recarrega o catálogo + o modpack ativo** (servidores/descrição) e atualiza o
  indicador de ligação. Sem mudanças no servidor. Compila 0/0.
- **Pendências:** nenhuma específica; o aviso recarrega metadados (não re-baixa mods de instâncias já
  instaladas — isso fica para o botão Jogar/Atualizar).

---

## [2026-06-29] ingest | Launcher: aba Instâncias + janelas do footer + auto-join

- **Fonte:** feedback do usuário (testando o launcher). Código: `TCMine-Launcher/Views/{InstancesPageView,LogWindow,MemoryWindow}.axaml(.cs)`,
  `ViewModels/{InstancesPageViewModel,MemoryEditViewModel}.cs`, `MainWindowViewModel.Play.cs` (export/import/delete/abrir-pasta),
  `HomePageViewModel` (auto-join), portas `IInstanceStore` (Export/Import/GameDir) + impl.
- **Páginas afetadas:** [[entities/tcmine-launcher]] (aba Instâncias + footer + auto-join), [[concepts/launcher-install-launch]].
- **Resumo:** ajustes pós-teste — (1) os botões do footer **Eventos**/**Memória** abrem **janelas**
  (borderless com chrome), não flyouts; (2) o botão por servidor na Home **marca o auto-join** (radio),
  e o **JOGAR** entra no servidor marcado (deixou de lançar por servidor); (3) **aba Instâncias**:
  lista das instaladas com **deletar, exportar (zip), importar, editar RAM** (janela) e **abrir pastas
  de shaders/texturas** (`shaderpacks`/`resourcepacks`). Confirmado: os **mods vêm do cache do servidor**
  (`/files/{fileId}/{fileName}`), nunca da API do CurseForge. Compila 0/0.
- **Pendências:** o rótulo de RAM no footer não refresca ao editar pela janela (cosmético); confirmação
  de delete sem diálogo; sync de configs ainda pendente.

---

## [2026-06-29] decisao | Launcher: Clean Architecture (infra dedicada) + Home estilo backup

- **Fonte:** pedido do usuário (foto da Home + arquitetura limpa); refactor do `TCMine-Launcher`. Novo
  projeto `TCMine-Launcher.Infrastructure` (no `.slnx`); `TCMine-Domain/Launcher/`,
  `TCMine-Application/Launcher/`; `Views/HomePageView.axaml` (redesenho) + `Behaviors/ImageLoader.cs`.
- **Páginas afetadas:** [[decisions/launcher-clean-architecture]] (nova), [[entities/tcmine-launcher-infrastructure]]
  (nova), [[sources/2026-06-29-launcher-clean-architecture]] (nova), [[entities/tcmine-launcher]]
  (só UI+composição), [[index]] (8 projetos).
- **Resumo:** o launcher passou a **espelhar o servidor** em Clean Architecture — models →
  `TCMine-Domain/Launcher`, **portas** → `TCMine-Application/Launcher`, impls → **`TCMine-Launcher.Infrastructure`**
  (CmlLib/HTTP/filesystem/fNbt); `TCMine-Launcher` fica só com Views/ViewModels + composição (Splat). A
  infra do launcher é **dedicada** (não o `TCMine-Infrastructure` partilhado) para não acoplar CmlLib ao
  servidor nem EF/Docker ao launcher. **Home** redesenhada no estilo da foto (hero + perfil/ID/servidores
  com ping e play-por-servidor). **Comportamento:** clicar num modpack só **seleciona** (sem download);
  instalar/lançar é o botão da Home. Mods continuam a vir do **cache do servidor** (`/files`). Servidor,
  infra e launcher compilam 0/0; a solução tem **8 projetos**.
- **Pendências:** validação em execução (lançar o jogo end-to-end); aba **Instâncias** dedicada (a grelha
  de instaladas saiu da Home, que segue a foto); só NeoForge; sem sync de configs.

---

## [2026-06-29] ingest | Launcher: instalar + lançar modpack (NeoForge)

- **Fonte:** sessão de implementação; referência `P:\TCMine-Launcher-bk`. Novos em `TCMine-Launcher/`:
  `Services/{LaunchOrchestrator,GameLauncher,ModInstaller,OverridesInstaller,ServersDatWriter,InstanceStore,SettingsStore,GameRunStateStore,GameLogCapture,MinecraftServerPinger,SystemInfo,HttpClientProvider}.cs`,
  `Models/{InstalledModpack,LaunchProgress,LauncherSettings,PlayerDataProfile}.cs`,
  `ViewModels/{MainWindowViewModel.Play,HomePageViewModel,SettingsPageViewModel}.cs` (+ `ModpacksPageViewModel`),
  `Views/{HomePageView,SettingsPageView}.axaml` (+ `ModpacksPageView`).
- **Páginas afetadas:** [[concepts/launcher-install-launch]] (nova), [[sources/2026-06-29-launcher-install-launch]]
  (nova), [[entities/tcmine-launcher]] (login+catálogo → +instalar/lançar), [[index]].
- **Resumo:** a função central do launcher — instalar e lançar um modpack **oficial NeoForge**. Pipeline:
  manifesto (`ApiClient`) → registo (`InstalledModpack`, 1 por modpack) → `LaunchOrchestrator`
  (NeoForge via CmlLib + mods com cache, **sem Sha1** + overrides `.zip` + `servers.dat` via fNbt) →
  `Process.Start` + captura de log + deteção de jogo aberto. **Página Jogar** (hero + botão grande +
  progresso + servidores com ping + grelha de instaladas) e **Definições** (slider de RAM até à RAM
  física + caminho do Java). Cards de Modpacks: Instalar/Jogar/Atualizar. Pacotes: `CmlLib.Core.Installer.NeoForge`,
  `fNbt`. Compila 0/0.
- **Pendências:** só NeoForge (outros loaders → erro amigável); sem sync de configs do jogador (falta
  `GET` no servidor); sem página de Instâncias dedicada. **Validação end-to-end** (lançar o jogo de
  facto) ainda por fazer — precisa do `Client.props` com `MicrosoftClientId`.

---

## [2026-06-29] decisao | Paleta dark do ColorTokens vira fria/azulada

- **Fonte:** preferência do usuário (achou a combinação do launcher v1 mais bonita); código
  `TCMine-Design/ColorTokens.cs` (`Dark.Background`/`Dark.Text`).
- **Páginas afetadas:** [[concepts/design-tokens]], [[entities/tcmine-design]] (impacto), [[index]].
- **Resumo:** os **neutros do tema dark** do `ColorTokens` mudaram de **quente** (preto-amarronzado:
  `Page #0D0B09`, `Border #3D362F`, `Text #F5F1ED`) para **frio/azulado** (`Page #0B0B14`,
  `Border #242438`, `Text #E8E8F0`/`#94A3B8`), reproduzindo a paleta do backup. O **laranja da marca**
  (`Primary`/`Accent` `#F97316`) ficou intacto. Decisão de **alcance total**: como o launcher
  (`AvaloniaTheme`) e o admin (`MudThemeFactory`) derivam do `ColorTokens`, **ambos** ficam azulados —
  mantém a fonte única de cor. Compila 0/0.
- **Pendências:** validar o admin (MudBlazor) com os neutros novos; tema `Light` não foi tocado.

---

## [2026-06-29] ingest | Launcher: shell da UI igual ao backup (chrome/sidebar/status)

- **Fonte:** pedido do usuário ("interface igual ao backup"); referência `P:\TCMine-Launcher-bk`
  (Views/Windows/MainWindow, Controls/{WindowChrome,TitleBar}, Themes/{Icons,WindowChrome,Styles}).
  Novos arquivos: `TCMine-Launcher/Views/{WindowChrome.cs,TitleBar.axaml(.cs),PlaceholderPageView.axaml(.cs)}`,
  `Themes/{Icons,WindowChrome}.axaml` + `Themes/Styles/{Buttons,Cards,Text,Controls}.axaml`,
  `Converters/IconKeyConverter.cs`, `ViewModels/PlaceholderPageViewModel.cs`; reescritos `MainWindow.axaml`,
  `LoginView.axaml`, `ModpacksPageView.axaml`, `MainWindowViewModel.cs`, `App.axaml`, `Theme/AvaloniaTheme.cs`.
- **Páginas afetadas:** [[entities/tcmine-launcher]] (shell replicado), [[concepts/design-tokens]].
- **Resumo:** replicado o **shell** do launcher v1 — janela borderless (`WindowChrome` arredondado +
  `TitleBar` com arrasto/min/fechar), **sidebar** de navegação por ícones com estado ativo, página com
  `TransitioningContentControl` (CrossFade) e **barra de estado** (ponto de ligação ao servidor + versão).
  O login (MSAL) passou a viver no `MainWindowViewModel` (a `LoginView` liga-se a ele, como no backup);
  páginas ainda sem feature usam um `PlaceholderPageViewModel` único. **Cor**: o `AvaloniaTheme` passou a
  emitir os aliases semânticos do backup (`BgSidebar`, `Accent`, `Danger`, …) **a partir do `ColorTokens`**
  — nenhum hex literal; tudo via `{DynamicResource}`. Compila 0/0.
- **Pendências:** validação visual em execução (launcher estava rodando build antigo durante a edição);
  as páginas de Jogar/Instâncias/Novidades/Definições são placeholders até as features existirem.

---

## [2026-06-29] ingest | Launcher: tema ColorTokens + URL injetada no build (refino)

- **Fonte:** feedback do usuário; código `TCMine-Launcher/Theme/AvaloniaTheme.cs` (restaurado),
  `Services/{AppConfig,ServerConfig}.cs`, `Themes/Styles/{Buttons,Cards,Text}.axaml`, `App.axaml(.cs)`,
  `Views/{LoginView,ModpacksPageView,MainWindow}.axaml`; design de referência em
  `P:\TCMine-Launcher-bk\TCMine-Launcher\Themes`.
- **Páginas afetadas:** [[entities/tcmine-launcher]] (tema + URL deixam de ser pendências),
  [[concepts/design-tokens]] (consumidor Avalonia restaurado), [[index]].
- **Resumo:** três ajustes pedidos. (1) **Cores do TCMine**: re-introduzido `AvaloniaTheme.ApplyTheme`
  que injeta os tokens de [[entities/tcmine-design]] (`ColorTokens`) como recursos Avalonia; estilos e
  views passam a usar `{DynamicResource}` dos tokens, nunca hexes. (2) **URL não-hardcoded**: `AppConfig`
  lê `TcmineServerUrl` de um `AssemblyMetadataAttribute` injetado no build (servidor injeta a URL/IP ao
  compilar o launcher; dev usa `Client.props`/fallback) — mesmo padrão do backup. (3) **Design do
  backup** seguido (card de login, botão Microsoft, cartões de modpack, chip de conta), mapeado para os
  tokens. Compila 0 erros/0 avisos.
- **Pendências:** instalar/lançar (próximo incremento); o **build do launcher pelo servidor** (que
  injeta `TcmineServerUrl` e o feed Velopack) ainda não existe.

---

## [2026-06-29] decisao | Launcher: login Microsoft pelo servidor + catálogo

- **Fonte:** sessão de implementação; código vivo `TCMine-Infrastructure/Minecraft/MicrosoftAuthService.cs`,
  `TCMine-Infrastructure/Identity/PlayerSessionService.cs`, `TCMine-Server/Services/LoginSessionBroker.cs`,
  `TCMine-Server/Endpoints/AuthEndpoints.cs`, `TCMine-Domain/Entities/PlayerAccountEntity.cs`,
  `TCMine-Launcher/Services/` e `ViewModels/`.
- **Páginas afetadas:** [[decisions/server-brokered-microsoft-login]] (nova),
  [[sources/2026-06-29-launcher-login-catalogo]] (nova), [[entities/tcmine-launcher]] (stub → wip),
  [[entities/tcmine-server]] (endpoints + decisão de auth), [[index]] (atualizado).
- **Resumo:** primeiro incremento real do launcher — **login + catálogo**. O login Microsoft do jogador
  é **orquestrado pelo servidor** (Auth Code + PKCE; cadeia Xbox→XSTS→Minecraft no `MicrosoftAuthService`),
  com o resultado empurrado ao launcher por uma **"live link" SSE direcionada** (`LoginSessionBroker`,
  keyed por `loginId` — vs. o broadcast do `/events`). O refresh token MS fica **cifrado no servidor**
  (`PlayerAccountEntity`, Data Protection); o launcher guarda só uma **sessão TCMine opaca** (DPAPI).
  Migração `PlayerAccount` nos dois providers. Launcher reusa os DTOs `record` do core. Servidor e
  launcher compilam (0 erros).
- **Pendências:** registar o redirect URI `{PublicBaseUrl}/auth/microsoft/callback` no Azure (setup
  externo); install/launch do jogo fora de escopo; tema `ColorTokens` do launcher por re-introduzir;
  `ServerConfig.BaseUrl` por tornar configurável. **Validação end-to-end exige reiniciar o servidor**
  (estava rodando o build antigo durante a implementação).

---

## [2026-06-27] ingest | Instâncias de servidor (Docker) + remodelagem da UX admin

- **Fonte:** sessão de implementação; código vivo `TCMine-Infrastructure/ServerInstances/`,
  `TCMine-Server/Components/Pages/Admin/` (Modpacks e Servers), `TCMine-Domain/Entities/`.
- **Páginas afetadas:** [[decisions/server-instances-docker]] (nova), [[concepts/server-instance-lifecycle]] (nova),
  [[concepts/modpack-server-hub-ux]] (nova), [[sources/2026-06-27-server-instances-e-ux]] (nova),
  [[index]] (atualizado).
- **Resumo:** recurso de **servidores Minecraft gerenciados** em containers Docker (DooD): cache de
  loader compartilhado, provisionamento com symlinks de mods/libraries, console/logs, reconciliação de
  status, ping de jogadores e trava de início. **Remodelagem da UX**: o editor de modpack em abas virou
  um **hub** (overview) com páginas/modais; ligação modpack↔servidor (auto-divulgação no launcher) e
  sincronização de "desatualizado" com "aplicar atualização" num clique. Monaco como padrão para editar
  arquivos (incl. configs do servidor). Correções: timeout do client Docker, `Mount` no Windows, reuso
  da imagem do release com JRE 25, resolução de dependências de mod, detecção de incompatibilidade.
- **Pendências:** ping/auto-divulgação dependem de endereço alcançável (em DooD pode exigir
  `host.docker.internal`); detecção de "boot pronto" do MC é aproximada (status vira Running ao subir o
  container, não ao fim do load). Loaders além de NeoForge (Forge/Fabric/Quilt) ainda não implementados.

---

## [2026-06-25] ingest | Página de novidades globais + seletor de modpack opcional

- **Fonte:** pedido do usuário; código `Admin/News/News.razor(.cs)`, `ModpackNewsService.cs`, `Dialogs/NewsEditDialog.razor`, `AdminLayout.razor`.
- **Páginas afetadas:** [[entities/tcmine-server]], [[concepts/modpack-admin-editor]].
- **Resumo:** nova página `/admin/news` (Owner/Admin) lista **todas** as novidades
  (globais + de modpacks) em `MudDataGrid` (padrão de listas) com coluna de destino
  (chip "Global" ou nome do modpack). O `NewsEditDialog` ganhou um **seletor de modpack
  opcional** (vazio = global, selecionado = do modpack) — exibido só quando o chamador
  passa as opções (`Modpacks`); a aba do editor segue sem seletor (modpack fixo). Serviço:
  `ListAllAsync` (com nome do modpack via subconsulta), `ListModpackOptionsAsync`,
  `CreateAsync(draft)` (ModpackId opcional) e `UpdateAsync` passou a gravar o vínculo. Link
  "Novidades" habilitado no menu. Build limpo. (Sem migração — `NewsEntity.ModpackId` já era
  FK opcional.)
- **Pendências:** nenhuma.

## [2026-06-25] meta | Padrão único de listas (MudDataGrid) + Novidades migrada

- **Fonte:** pedido do usuário; `CLAUDE.md` + `Panels/NewsPanel.razor(.cs)`.
- **Páginas afetadas:** `CLAUDE.md` (nova seção "Listas"), [[concepts/modpack-admin-editor]].
- **Resumo:** nova **regra** no `CLAUDE.md`: toda lista do painel usa `MudDataGrid` no
  mesmo modelo (referência: página de Mods) — `ToolBarContent` com busca via
  `QuickFilter`, `MudDataGridPager` (25/50/100), colunas enxutas, sem `Virtualize` nem
  paginação manual; exceção para listas editáveis inline (ex.: `ServersPanel`). A aba
  **Novidades** (`NewsPanel`) foi migrada de cards + `MudPagination` para esse padrão.
  Build limpo.
- **Pendências:** `ServersPanel` segue como cards editáveis (exceção registrada na regra).

## [2026-06-25] ingest | Lista de modpacks: tabela enxuta (padrão da página de Mods) + busca + pager

- **Fonte:** pedido do usuário; código `TCMine-Server/Components/Pages/Admin/Modpacks/Modpacks.razor(.cs)`.
- **Páginas afetadas:** [[concepts/modpack-admin-editor]].
- **Resumo:** a listagem (antes `MudDataGrid` de 10 colunas virtualizado sem pager) foi
  reescrita no **mesmo padrão da página de Mods**: `MudDataGrid` com `ToolBarContent`
  (busca por nome/Minecraft via `QuickFilter`) + **`MudDataGridPager`** (25/50/100), e
  colunas **enxutas** (Nome+versão na mesma célula, Minecraft, Loader, Mods, Status,
  Atualizado, ações) — removidas Servidores/Overrides para ocupar menos espaço. (Primeiro
  tentei grade de cards; o usuário preferiu tabela no padrão de Mods.) Build limpo.
- **Pendências:** nenhuma.

## [2026-06-25] decisao | Origem CF + checagem econômica de atualizações (modpack e mods)

- **Fonte:** [[sources/2026-06-25-curseforge-update-tracking]] (pedido do usuário + código vivo).
- **Páginas afetadas:** [[decisions/curseforge-update-tracking]] (nova), [[sources/2026-06-25-curseforge-update-tracking]] (nova), [[concepts/modpack-admin-editor]], [[entities/tcmine-server-infrastructure]], `index.md`.
- **Resumo:** modpacks importados do CF ganham a tabela 1:1 `ModpackImportSources`
  (versão importada + cache de update). `CheckModpackUpdateAsync` (TTL 6h, reusa
  `GetLatestFileAsync`) e `CheckModUpdatesAsync` (**sob demanda**, batch
  `latestFilesIndexes` + batch files) economizam API: ~2 chamadas para checar todos os
  mods, 1 para o modpack. UI: banner de origem no editor (verificar/atualizar) + botão
  "Buscar atualizações" na aba Mods com `ModUpdatesDialog`. Atualizar preserva
  `Side`/`Target` (merge). Migração `ModpackImportSource` nos dois providers. Build limpo.
- **Pendências:** job diário opcional para popular o cache de update dos modpacks
  publicados (badge sem clique) ficou como futuro.

## [2026-06-25] ingest | Refactor do ModpackEditor em componentes por aba + paginação

- **Fonte:** pedido do usuário; código `TCMine-Server/Components/Pages/Admin/Modpacks/` (`ModpackEditor.razor(.cs)`, novo `Panels/`).
- **Páginas afetadas:** [[concepts/modpack-admin-editor]].
- **Resumo:** o `ModpackEditor` (markup monolítico de ~350 linhas) foi decomposto: cada
  aba virou um componente em `Panels/` — `DetailsPanel` (metadados + versões,
  self-contained no `MinecraftVersionService`), `ModsPanel` (toolbar + tabela paginada
  + filtro; ações via `EventCallback`), `ServersPanel` (lista paginada editável),
  `NewsPanel` (newsletter self-contained no `ModpackNewsService`, paginada; substitui o
  antigo `ModpackEditor.News.cs`, removido). O editor agora **orquestra**: segura
  `_draft`/`_mods`, Guardar, troca de aba; cabeçalho moderno com chips de status. Todas
  as listas ganharam **paginação** (Mods/News/Servers). Build limpo, sem avisos novos.
- **Pendências:** nenhuma.

## [2026-06-25] fix | Arquivos apareciam como pasta (chevron) no lazy loading

- **Fonte:** relato do usuário; código `OverridesPanel.razor`.
- **Resumo:** no modo lazy, todo nó mostrava chevron e arquivos não abriam para editar.
  Causa: o `MudTreeViewItem` recebia `Expandable` — que **não é parâmetro** dele (era
  ignorado como atributo HTML, daí o aviso MUD0002) — então o chevron seguia o padrão
  `CanExpand=true`. Trocado para **`CanExpand="@node.Expandable"`** (false em arquivo,
  true em pasta): arquivos perdem o chevron; pastas expandem via `ServerData`. Aviso
  MUD0002 também resolvido. **Abrir no editor:** o `BodyContent` customizado não dispara
  a seleção do `MudTreeView`, então o clique no nome passou a ser tratado por um
  `@onclick` próprio (`OnNodeClick` → abre arquivo / ignora pasta), setando `_selected`
  para o highlight.

## [2026-06-25] ingest | Lazy loading da árvore de overrides (ServerData)

- **Fonte:** pedido do usuário; código `ModpackImportService.cs` (`ListOverrideChildren`), `OverridesPanel.razor(.cs)`, `OverrideTreeBuilder.cs`, `Contracts/Modpack.cs`.
- **Páginas afetadas:** [[concepts/modpack-admin-editor]].
- **Resumo:** a árvore deixou de montar tudo de uma vez (`ListOverrideFiles` recursivo
  + `OverrideTreeBuilder.Build`). Agora a **raiz** é semeada em `Items` (no init) e os
  **filhos diretos** de cada pasta vêm do **`MudTreeView.ServerData`** ao expandir, via
  novo `ListOverrideChildren(uid, folder)` (um nível) + DTO `OverrideNodeDto`.
  _(Correção: o `ServerData(null)` automático não semeava a raiz — a primeira tentativa,
  só-ServerData, não carregava nada; semear `Items` resolveu.)_ `_fileSet`
  vira incremental (preenchido ao abrir cada pasta) — a detecção arquivo×pasta e a
  seleção/edição continuam válidas para nós visíveis. Mudanças estruturais resetam a
  árvore (bump `_treeKey`) para o `ServerData` reler; o Save (só conteúdo) não reseta.
  Removidos `Build`/`Sort` do `OverrideTreeBuilder` (sobra só `FileIcon`). Build limpo.
- **Pendências:** nenhuma. (Com lazy loading, operações estruturais colapsam a árvore
  para a raiz — comportamento esperado.)

## [2026-06-25] fix | Árvore de overrides não atualizava após desfazer

- **Fonte:** relato do usuário; código `OverridesPanel.razor(.cs)`.
- **Páginas afetadas:** [[concepts/modpack-admin-editor]].
- **Resumo:** após "Desfazer" (e reverter pelo histórico) a árvore não refletia o
  novo estado — o `Service.UndoLastAsync` revertia em disco e `ReloadAsync` relê, mas
  a reconciliação **in-place** do `MudTreeView` não reflete bem a volta dos nós aos
  caminhos anteriores. Corrigido com um `@key` (`_treeKey`) bumpado em
  `ReloadAsync(forceTreeRebuild: true)` no desfazer/histórico, forçando a árvore a
  reconstruir. As demais operações (novo/upload/apagar/mover) seguem com update
  in-place (preservam a expansão).
- **Pendências:** nenhuma.

## [2026-06-25] ingest | Drag-and-drop para mover overrides na árvore

- **Fonte:** pedido do usuário; código `TCMine-Server/Components/Pages/Admin/Modpacks/OverridesPanel.razor(.cs/.css)`, `wwwroot/js/overrides-dnd.js`, `Components/App.razor`.
- **Páginas afetadas:** [[concepts/modpack-admin-editor]].
- **Resumo:** além do botão de mover, a árvore de overrides agora suporta
  **drag-and-drop** (eventos HTML5 `draggable`/`ondragstart`/`ondragover:preventDefault`/
  `ondrop`). Soltar sobre pasta move pra dentro; sobre arquivo move pra pasta-pai; na
  área vazia da `MudPaper` move pra raiz. Payload em memória (`_dragPath`),
  `stopPropagation` no drop dos itens pra não duplicar com o drop da raiz. Botão e DnD
  compartilham `MoveToFolderAsync`.
- **Correção (destaque com delay):** o highlight do alvo era via `@ondragenter`
  (Blazor Server) — cada evento ia ao servidor e voltava, causando **atraso grande**.
  Movido para **JS client-side** (`wwwroot/js/overrides-dnd.js`, delegação no
  `document` togglando `.ovr-drop`), instantâneo; só `@ondrop`/`@ondragstart` seguem no
  servidor (uma vez por arraste). Build sem erros/avisos novos.
- **Pendências:** nenhuma.

## [2026-06-25] ingest | Paginação nas listas de mods + botão de mover na árvore de overrides

- **Fonte:** pedido do usuário; código `TCMine-Server/Components/Pages/Admin/Modpacks/{ModpackEditor.razor,OverridesPanel.razor,OverridesPanel.razor.cs}`, `Admin/Mods/Mods.razor`, `Dialogs/OverridePathDialog.razor`.
- **Páginas afetadas:** [[concepts/modpack-admin-editor]].
- **Resumo:** (1) **Paginação**: a `MudTable` de mods do editor ganhou `MudTablePager`
  (25/50/100) e a `MudDataGrid` de "todos os mods" trocou `Virtualize` por
  `MudDataGridPager` (25/50/100). (2) **Mover na árvore de overrides**: `MudTreeView`
  com `ExpandOnClick=false` e `BodyContent` por item — três ações: chevron expande
  (pasta), nome abre no editor, botão à direita move (`stopPropagation` para não
  selecionar). O move reusa o `OverridePathDialog` (novo parâmetro `AllowEmpty` p/
  destino raiz) e chama `MoveOverrideAsync`/`MoveOverrideFolderAsync` (que já existiam,
  com histórico/desfazer) sob o `BusyService`. Build sem erros novos.
- **Pendências:** nenhuma. (Aviso pré-existente `MudForm.Validate` obsoleto no
  `UserEditDialog` segue de fora deste escopo.)

## [2026-06-25] ingest | Marcador de mod órfão + página "todos os mods" com badges

- **Fonte:** pedido do usuário; código `TCMine-Infrastructure/Minecraft/ModpackImportService.cs`, `TCMine-Server/Components/Pages/Admin/Mods/`.
- **Páginas afetadas:** [[decisions/mods-many-to-many]], [[entities/tcmine-server]], [[entities/tcmine-server-infrastructure]].
- **Resumo:** complementos da normalização N:N. (1) **Marcador de órfão**:
  `ModFileEntity.OrphanedAt` (UTC), mantido por `MarkOrphansAsync` no `SaveAsync`
  (arquivos que saíram do pack) e no `DeleteAsync` (ao apagar modpack); limpo quando
  o arquivo é revinculado. Migration `ModFileOrphanMarker` nos dois providers. (2)
  **Página `/admin/mods`** (Owner/Admin): `MudDataGrid` com todos os `ModFile`,
  coluna de **badges dos modpacks** em que aparecem, chip de origem (CF/Manual),
  filtro textual + switch "só órfãos", e apagar (`DeleteOrphanFileAsync`, só órfãos —
  remove o `ModFile` e o jar do cache). Link no menu do `AdminLayout`. Build limpo.
- **Pendências:** nenhuma; GC de órfãos agora é manual pela página (automatizar no
  futuro, se desejado).

## [2026-06-25] decisao | Mods em N:N (ModFile + ModpackMod) em vez de FK 1:N

- **Fonte:** [[sources/2026-06-25-mods-many-to-many]] (pergunta do usuário sobre o schema + refactor no código vivo).
- **Páginas afetadas:** [[decisions/mods-many-to-many]] (nova), [[sources/2026-06-25-mods-many-to-many]] (nova), [[concepts/modpack-mods-locais]], [[entities/tcmine-domain]], [[entities/tcmine-server-infrastructure]], `index.md`.
- **Resumo:** o usuário notou que a FK `ModpackId` em `Mods` duplicava linhas do
  mesmo arquivo entre modpacks. Normalizado para **N:N**: `ModFileEntity` (PK
  `FileId`, metadados uma vez) + `ModpackModEntity` (junção `(ModpackId, FileId)`
  com `Side`/`Target`/`SortOrder`, por-modpack). `ModEntryEntity` virou modelo plano
  (não-EF) de rascunho/import; `SaveAsync` faz upsert de `ModFile` + reconcile dos
  vínculos; `FlattenMods` reidrata o editor. Manifesto, `ContentCatalog` (counts) e o
  editor Blazor ajustados. Migrations `ModsManyToMany` geradas nos dois providers.
  Build da solução limpo.
- **Pendências:** migration **destrutiva** (dropa `Mods` — dados de mod perdidos no
  migrate; jars no cache repovoam via re-save) — avisar antes de aplicar em dados
  reais. GC de `ModFile` órfão fica como trabalho futuro.

## [2026-06-25] ingest | Overlay aparece primeiro, troca de aba com feedback e fix do MudFileUpload

- **Fonte:** pedido do usuário nesta sessão; código `TCMine-Server/Services/BusyService.cs`, `Components/Pages/Admin/Modpacks/{ModpackEditor.razor,ModpackEditor.razor.cs,OverridesPanel.razor}`.
- **Páginas afetadas:** [[concepts/async-feedback-overlay]], [[concepts/modpack-admin-editor]].
- **Resumo:** três ajustes finos. (1) `BusyService.RunAsync` agora faz
  `Begin` → **`await Task.Yield()`** → operação, garantindo que o overlay seja
  enviado ao cliente **antes** do trabalho (a modal é a primeira coisa visível). (2)
  **Troca de aba do `ModpackEditor`** passou a mostrar o overlay: `MudTabs`
  controlado (`ActivePanelIndex="_activeTab"`) + `OnPreviewInteraction` que cancela a
  ativação nativa e a refaz dentro de `Busy.RunAsync` — resolve o travamento das abas
  Mods/Overrides com muitos itens (o clique parecia morto). (3) Corrigido o
  `MudFileUpload`: `ActivatorContent` (legado, sinalizado pelo Rider) → **`CustomContent`**
  com `context.OpenFilePickerAsync()`, no `ModpackEditor` e no `OverridesPanel`.
- **Pendências:** nenhuma. Verificado em runtime (funciona). Para robustez, a troca de
  aba agora é conduzida via `@ref` + `MudTabs.ActivatePanelAsync(target, false)` dentro
  do `Busy.RunAsync` (com guard `_switchingTab` contra reentrância), além do resync do
  parâmetro `ActivePanelIndex` — dois caminhos garantindo a troca sob o overlay.

## [2026-06-25] ingest | Overlay bloqueante de feedback em toda operação async do painel

- **Fonte:** pedido do usuário nesta sessão; [[sources/2026-06-25-busy-overlay]] (código vivo `TCMine-Server/Services/BusyService.cs`, `Components/Shared/BusyOverlay.razor` e consumidores em `Components/Pages/Admin/`).
- **Páginas afetadas:** [[concepts/async-feedback-overlay]] (nova), [[sources/2026-06-25-busy-overlay]] (nova), [[entities/tcmine-server]]; `CLAUDE.md` (Parte I).
- **Resumo:** o usuário pediu um **modal não-fechável** de feedback em toda operação
  async/banco (atual e futura). Implementado como `BusyService` (scoped, contador de
  operações) + `BusyOverlay` (`MudOverlay` único no `RootLayout`), com helper
  `Busy.RunAsync("msg", op)`. Aplicado a loads e mutações de Users, Modpacks,
  ModpackEditor (+News, +Overrides), Settings e Dashboard; skeletons das listas
  removidos. Por escolha do usuário, o escopo é "toda operação async", com exceções
  de UX (refresh recorrente do `SystemStatusCard`, buscas internas de diálogos,
  fluxos com progresso dedicado import/Save, micro-leituras de seleção). Convenção
  registrada no `CLAUDE.md` (Parte I, nova seção "Feedback de operações async").
- **Pendências:** os skeletons dos widgets do dashboard foram mantidos (são
  fallbacks por parâmetro; o overlay já cobre o load da página) — reavaliar se
  vale removê-los também.

## [2026-06-25] ingest | Settings restrito ao Owner + run config de watch mode no Rider

- **Fonte:** pedido do usuário nesta sessão; código `TCMine-Server/Components/Pages/Admin/Settings.razor` e `.run/TCMine-Server (watch).run.xml`.
- **Páginas afetadas:** [[entities/tcmine-server]], [[concepts/setup-auth-cookie]].
- **Resumo:** dois itens. (1) **Segurança:** `Admin/Settings` ganhou
  `@attribute [Authorize(Roles = "Owner")]` — antes só o link do menu era
  só-Owner, mas a página era acessível por URL direta a qualquer autenticado
  (resolve a contradição registrada em [[concepts/setup-auth-cookie]]). (2)
  **DX:** criado run config compartilhável `.run/TCMine-Server (watch).run.xml`
  (tipo Shell Script, roda `dotnet watch run --launch-profile https` no terminal)
  para desenvolver com hot reload sem reiniciar o servidor a cada mudança. `.run/`
  não é ignorado pelo git (ao contrário de `.idea/`), então a config é versionável.
- **Pendências:** nenhuma para estes itens.

## [2026-06-25] ingest | Gestão de usuários (/admin/users) + regra de economia de tokens na wiki

- **Fonte:** pedido do usuário nesta sessão; código vivo `TCMine-Server/Components/Pages/Admin/Users/` e `TCMine-Infrastructure/Identity/UserService.cs`.
- **Páginas afetadas:** [[entities/tcmine-server]], [[concepts/setup-auth-cookie]]; `CLAUDE.md` (§8).
- **Resumo:** entregue a tela de **gestão de usuários** do painel (`/admin/users`,
  só `Owner`): página `Admin/Users/Users` (lista, toggle de ativo, remover) +
  `UserEditDialog` (criar/editar — login, papel, ativo, senha opcional na edição).
  `UserService` ganhou `UpdateAsync` e passou a **aplicar de fato** a proteção do
  último Owner ativo em `Update`/`SetActive`/`Delete` (antes só existia
  `CountActiveOwnersAsync`, sem uso), além de checar unicidade do login em
  `Create`/`Update`. Também adicionada ao `CLAUDE.md` (§8) a regra de **usar
  `tools/wikisearch.py`** ao mexer na wiki, para economizar tokens.
- **Pendências:** a nota antiga em [[concepts/setup-auth-cookie]] diz que `Settings`
  aceita qualquer admin, mas o `AdminLayout` já restringe o link ao Owner — revisar
  se o `[Authorize]` da página acompanha. CRUD de releases ainda pendente.

## [2026-06-25] ingest | Dashboard: nome do modpack na atividade + KPIs de novidades separados

- **Fonte:** pedido do usuário nesta sessão; código vivo `TCMine-Server/Components/Pages/Admin/Widgets/` e `TCMine-Infrastructure/Server/ContentCatalog.cs`.
- **Páginas afetadas:** [[entities/tcmine-server]].
- **Resumo:** dois ajustes no dashboard admin. (1) `RecentActivityCard` mostra o
  **nome do modpack** em vez do `Guid` — `ActivityItem` ganhou `ModpackName`,
  preenchido por subconsulta em `ContentCatalog` (não há navigation property em
  `OverrideHistoryEntry`); UI cai para o id quando o modpack foi excluído. (2) O
  KPI único de "Novidades" em `DashboardKpis` virou **dois**: *Novidades globais*
  (`News.ModpackId == null`) e *Novidades de modpacks* (`ModpackId != null`),
  alimentados pelos novos campos `GlobalNews`/`ModpackNews` de `DashboardData`
  (só notícias publicadas).
- **Pendências:** decidir se contagens devem incluir notícias não publicadas; o
  dashboard ainda não tem página/conceito próprio na wiki.

## [2026-06-24] meta | Nova regra: sem monolitos (dividir em arquivos menores)

- **Fonte:** instrução do usuário nesta sessão.
- **Páginas afetadas:** `CLAUDE.md` (Parte I, nova seção "Tamanho e
  responsabilidade dos arquivos"); aplicada já em [[concepts/modpack-admin-editor]].
- **Resumo:** adicionada regra obrigatória — **nunca criar monolitos; sempre
  dividir em arquivos menores com responsabilidade própria** (componentes Blazor
  por aba/seção, diálogos em arquivos próprios, partials por área, lógica de
  negócio fora da UI). Aplicada na hora ao refatorar a aba de overrides para o
  componente `OverridesPanel` + `OverrideTreeBuilder`.
- **Pendências:** revisitar outros arquivos grandes do servidor conforme forem
  tocados. Nada commitado (regra §11).

## [2026-06-24] ingest | Modpacks: import bloqueante, DataGrid virtualizado, MudTreeView, fix Monaco

- **Fonte:** código vivo escrito nesta sessão (ajustes pedidos pelo usuário).
- **Páginas afetadas:** [[concepts/modpack-admin-editor]] (onde vive, overrides,
  Monaco, BlazorMonaco 3.4.0), [[sources/2026-06-24-modpack-admin-ui]] (a manter).
- **Resumo:** (1) **import de modpack** agora abre `ImportProgressDialog` — modal
  de feedback **bloqueante** (sem fechar/ESC/backdrop) fechado por código ao
  terminar; (2) lista de modpacks migrada de `MudTable` para **`MudDataGrid`
  virtualizado** (`Virtualize`, `FixedHeader`, `Height`); (3) lista de overrides
  virou **`MudTreeView`** (árvore hierárquica via `OverrideTreeBuilder`); (4)
  **corrigido** o Monaco não abrir o arquivo — o `StandaloneCodeEditor` era
  renderizado só após a seleção (ref `null` no `SetValue`); agora fica sempre
  montado e aplica conteúdo pendente no `OnDidInit`. Aba de overrides extraída
  para `OverridesPanel` (regra sem-monolitos). BlazorMonaco bumpado p/ **3.4.0**.
  Build da solução: 0 erros.
- **Pendências:**
  - Mover arquivo/pasta de override na UI (serviço já suporta; árvore ainda só
    seleciona/edita/apaga).
  - UI do feed global de notícias. Nada commitado (regra §11).

## [2026-06-24] decisao | Newsletter por modpack — FK opcional em NewsEntity

- **Fonte:** decisão do usuário nesta sessão (escolha entre FK opcional / FK
  obrigatória / adiar) — optou por **FK opcional**.
- **Páginas afetadas:** [[concepts/modpack-admin-editor]] (seção Newsletter +
  abas + políticas de escrita), [[entities/tcmine-server]] (pendência fechada).
- **Resumo:** resolvido o gatilho §5 deixado em aberto. `NewsEntity.ModpackId`
  agora é `Guid?` (null = notícia global; preenchido = do modpack), com
  relacionamento `OnDelete(Cascade)` e índice em `ModpackId`. Migration
  `NewsModpackFk` gerada e verificada nos **dois** providers (SQLite TEXT /
  Postgres uuid) — contém só coluna + índice + FK (o snapshot já tinha o resto do
  modelo de modpack). Adicionados `ModpackNewsService` (CRUD direto no banco com
  `ContentNotifier.Bump()`), a aba **Novidades** no editor e o `NewsEditDialog`.
  Build da solução: 0 erros. Corrigido também um `continue` colado por engano na
  linha 1 do `ModpackEditor.razor`.
- **Pendências:**
  - UI para o **feed global** de notícias (FK nula) — ainda não existe.
  - Aplicar a migration ao banco de dev no próximo `dotnet run` (auto no boot).
  - Nada commitado (regra §11) — código + docs tocados; usuário decide o commit.

## [2026-06-24] ingest | UI admin de modpacks + BlazorMonaco

- **Fonte:** código vivo escrito nesta sessão ([[sources/2026-06-24-modpack-admin-ui]]);
  base no `ModpackImportService` já existente ([[entities/tcmine-server-infrastructure]]).
- **Páginas afetadas:** criado [[concepts/modpack-admin-editor]] e
  [[sources/2026-06-24-modpack-admin-ui]]; atualizado [[entities/tcmine-server]]
  (componentes, decisões, pendências, frontmatter); `index.md` atualizado.
- **Resumo:** entregue a camada Blazor de criação/edição de modpacks em
  `TCMine-Server/Components/Pages/Admin/Modpacks/` — lista (`Modpacks.razor`),
  editor em abas (`ModpackEditor` + parcial `.Overrides.cs`) e 4 diálogos. Cobre
  busca/import CurseForge, upload de jar, marcação `Side`/`Target` por mod, e
  edição de overrides com **BlazorMonaco 3.3.0** (adicionado à gestão central de
  pacotes, 3 scripts no `App.razor`). Confirmada a API da v3.3.0 contra a fonte
  oficial (`StandaloneCodeEditor`, `Global.SetModelLanguage(IJSRuntime,...)`).
  Build da solução: 0 erros. Mistura deliberada de duas políticas de escrita:
  rascunho-só-ao-Guardar (metadados/mods/servidores) vs. disco-imediato
  (overrides, com histórico/desfazer).
- **Pendências:**
  - **Newsletter por modpack** (pedida): contradiz o `NewsEntity` global —
    gatilho §5 (contradição + impacto amplo). Adiada à decisão do usuário sobre o
    modelo (FK em `NewsEntity` vs. entidade nova) + migrations nos dois providers.
  - Expor mover arquivo/pasta de override na UI (serviço já suporta).
  - Restringir `Settings` ao `Owner`; criar instância de servidor a partir do modpack.
  - Nada commitado (regra §11) — docs e código tocados; usuário decide o commit.

## [2026-06-23] ingest | Conceitos e decisões transversais (batch)

- **Fonte:** mesma leitura de código vivo ([[sources/2026-06-23-leitura-codigo-vivo]]),
  aprofundada com `Endpoints/{CurseForgeProxy,Modpack,Events,PlayerConfig}Endpoints.cs`,
  `Authentication/{AuthClaims,PersistingAuthenticationStateProvider}.cs`,
  `Infrastructure/Server/{ContentNotifier,ContentCatalog}.cs`,
  `Infrastructure/Minecraft/MinecraftAuthService.cs`,
  `Infrastructure/Identity/{UserService,SetupState}.cs`.
- **Páginas afetadas:** criados 11 conceitos ([[concepts/clean-architecture]],
  [[concepts/shared-domain-logic]], [[concepts/modside-rules]],
  [[concepts/dtos-as-records]], [[concepts/design-tokens]],
  [[concepts/curseforge-proxy]], [[concepts/modpack-mods-locais]],
  [[concepts/sse-content-sync]], [[concepts/setup-auth-cookie]],
  [[concepts/secrets-data-protection]], [[concepts/player-config-sync]]) e 2
  decisões ([[decisions/persistence-dual-provider]],
  [[decisions/central-package-management]]); `index.md` atualizado;
  [[sources/2026-06-23-leitura-codigo-vivo]] corrigida (não estão mais "por criar").
- **Resumo:** fechados os links que as entidades referenciavam. Cada página foi
  verificada contra o código atual — detalhes não-óbvios capturados: proxy `/v1`
  devolve 503 sem key e é passthrough transparente; manifesto filtra por
  `RunsOnClient` e reescreve URLs para `/files/...`; SSE usa canal limitado
  `DropOldest` + keep-alive 25s; `MinecraftAuthService` é **fail-open**; senhas em
  **PBKDF2** (`PasswordHasher`); secrets cifrados com protector
  `TCMine.ServerSettings.v1`, sem fallback de env var.
- **Pendências:**
  - `player-config-sync` em `status: wip`: só o PUT existe; o GET de leitura é
    citado no comentário mas não há rota — confirmar onde/se será servido.
  - Disparo de `ContentNotifier.Bump()` nas mutações depende do CRUD admin (a fazer).
  - Restringir `Admin/Settings` ao papel `Owner`.
  - Nada commitado (regra §11).

## [2026-06-23] ingest | Leitura inicial da solução TCMine (código vivo)

- **Fonte:** leitura de código vivo dos 7 projetos em `P:\TCMine` (branch
  `master`, HEAD `ab18cef`); resumo em [[sources/2026-06-23-leitura-codigo-vivo]].
- **Páginas afetadas:** criadas as entidades [[entities/tcmine-solution]],
  [[entities/tcmine-domain]], [[entities/tcmine-application]],
  [[entities/tcmine-server-infrastructure]], [[entities/tcmine-design]],
  [[entities/tcmine-server]], [[entities/tcmine-launcher]],
  [[entities/tcmine-icongenerator]]; criada a fonte
  [[sources/2026-06-23-leitura-codigo-vivo]]; `index.md` atualizado.
- **Resumo:** primeira ingestão de conteúdo. Semeada uma página por projeto, a
  partir da leitura do código atual (não da versão antiga commitada). Capturado o
  estado real: servidor com backend + admin (Dashboard, Settings) e settings de
  runtime cifradas (`ServerSettingsService`); launcher ainda **scaffolded**;
  serviços Minecraft e orquestração de `ServerInstance` modelados mas não
  operados. Sem contradições (wiki estava vazia). Também re-adicionada a
  ferramenta `tools/wikisearch.py` (a pedido) e referenciada no `CLAUDE.md`
  (§3 e §8 do schema).
- **Pendências:**
  - Criar as páginas de `concepts/` e `decisions/` referenciadas pelas entidades
    (hoje links não resolvidos): clean-architecture, design-tokens,
    shared-domain-logic, modside-rules, curseforge-proxy, modpack-mods-locais,
    sse-content-sync, setup-auth-cookie, player-config-sync,
    secrets-data-protection, dtos-as-records, persistence-dual-provider,
    central-package-management.
  - Aprofundar entidades em `status: stub/wip` conforme o código evoluir.
  - Nada commitado (regra §11).

## [2026-06-23] setup | Criação do sistema de wiki TCMine-Docs

- **Fonte:** prompt de setup do usuário (sessão de 2026-06-23).
- **Páginas afetadas:** estrutura inicial criada — `CLAUDE.md` (raiz, schema
  mestre), `wiki/index.md`, `wiki/log.md`, templates (`entity`, `concept`,
  `decision`, `source`), pastas `raw/assets/`, `wiki/entities/`,
  `wiki/concepts/`, `wiki/decisions/`, `wiki/sources/`.
- **Resumo:** Criada do zero a base de conhecimento LLM-mantida em
  `TCMine-Docs/`, no mesmo repositório git da solução TCMine. Decisões tomadas
  com o usuário nesta sessão:
  1. **Start fresh** — não restaurar a versão anterior commitada em `252a8c1`
     (17 páginas + ferramenta de busca + vault Obsidian); ela permanece no
     histórico git se um dia for necessária.
  2. **Estrutura em inglês com `decisions/` separado** — categorias
     `entities/concepts/decisions/sources/templates`; decisões viram ADRs
     próprios, distintos dos conceitos.
  3. **Ingest autônomo** por padrão, com confirmação só em três gatilhos
     (contradição / impacto amplo / categorização incerta).
  4. **Health-check automático no boot** de cada sessão (report-only; correções
     além do trivial exigem ok do usuário).
  5. **`CLAUDE.md` único na raiz** com a constituição completa da wiki + o
     básico do projeto (idioma, comandos, arquitetura); sem `CLAUDE.md` próprio
     em `TCMine-Docs/`.
- **Pendências:**
  - Primeira ingestão real de conteúdo (artigo, código vivo ou export de chat) —
    a wiki está vazia.
  - Nada commitado (regra §11). A working tree contém a remoção dos arquivos
    antigos de `TCMine-Docs/` e do `CLAUDE.md` anterior, mais os arquivos novos
    desta sessão — o usuário decide como commitar.
