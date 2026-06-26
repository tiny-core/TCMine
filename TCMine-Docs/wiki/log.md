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
- **Páginas afetadas:** [[decisions/curseforge-update-tracking]] (nova), [[sources/2026-06-25-curseforge-update-tracking]] (nova), [[concepts/modpack-admin-editor]], [[entities/tcmine-infrastructure]], `index.md`.
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
- **Páginas afetadas:** [[decisions/mods-many-to-many]], [[entities/tcmine-server]], [[entities/tcmine-infrastructure]].
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
- **Páginas afetadas:** [[decisions/mods-many-to-many]] (nova), [[sources/2026-06-25-mods-many-to-many]] (nova), [[concepts/modpack-mods-locais]], [[entities/tcmine-domain]], [[entities/tcmine-infrastructure]], `index.md`.
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
  base no `ModpackImportService` já existente ([[entities/tcmine-infrastructure]]).
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
  [[entities/tcmine-infrastructure]], [[entities/tcmine-design]],
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
