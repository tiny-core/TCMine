---
type: concept
title: Editor de modpacks (painel admin)
tags: [concept, modpack, blazor, admin, mudblazor, overrides]
status: stable
created: 2026-06-24
updated: 2026-06-24
aliases: [editor de modpack, ModpackEditor, página de modpacks]
sources:
  - "[[sources/2026-06-24-modpack-admin-ui]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/modside-rules]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[decisions/central-package-management]]"
---

# Editor de modpacks (painel admin)

> A UI Blazor (MudBlazor) sobre o `ModpackImportService`: cria/edita modpacks,
> mescla conteúdo do CurseForge, faz upload de jars e edita overrides com Monaco.

## Onde vive

- `TCMine-Server/Components/Pages/Admin/Modpacks/`
  - `Modpacks.razor` (+ `.razor.cs`) — rota `/admin/modpacks`: catálogo em **`MudDataGrid`**
    no mesmo padrão da página de Mods (toolbar com busca + `MudDataGridPager` 25/50/100),
    com colunas **enxutas** (Nome+versão, Minecraft, Loader, Mods, Status, Atualizado, ações).
    Restrito a `Owner,Admin`.
  - `ModpackEditor.razor` (+ `.razor.cs`) — rotas `/admin/modpacks/new` e
    `/admin/modpacks/{Id}`: **orquestrador** em abas. Mantém o rascunho
    (`_draft` + lista plana `_mods`), o Guardar e a troca de aba; cada aba é um
    componente próprio. O cabeçalho moderno reúne título + chips de status
    (publicado/rascunho, Minecraft, loader, nº de mods) + botão Guardar.
  - `Panels/` — um componente por aba (regra **sem monolitos**):
    `DetailsPanel` (metadados + seletores de versão, self-contained no
    `MinecraftVersionService`), `ModsPanel` (toolbar + tabela paginada + filtro;
    ações via `EventCallback`), `ServersPanel` (lista paginada editável),
    `NewsPanel` (newsletter self-contained no `ModpackNewsService`, paginada).
  - `OverridesPanel.razor` (+ `.razor.cs`) — componente próprio da aba Overrides
    (árvore lazy + Monaco). `OverrideTreeBuilder.cs` só mapeia ícone por extensão.
  - `Dialogs/` — `CurseForgeSearchDialog` (busca mods, multi-seleção),
    `ImportModpackDialog` (busca modpack), `ImportProgressDialog` (feedback
    bloqueante durante o import), `OverridePathDialog`, `OverrideHistoryDialog`,
    `NewsEditDialog`.

O backend é o `ModpackImportService` ([[entities/tcmine-infrastructure]]); a UI
**não** fala com EF/CurseForge direto — só com esse serviço. A decomposição em
componentes/partials/diálogos segue a regra **sem monolitos** do `CLAUDE.md`.

## Abas do editor

1. **Detalhes** — nome, versão, Minecraft + loader + versão do loader (seletores
   `MudAutocomplete` alimentados pelo `MinecraftVersionService`, com texto livre
   no fallback), descrição, RAM recomendada, switch de publicado.
2. **Mods** — `MudTable` **paginada** (`MudTablePager`, 25/50/100) com `Target`
   (mod/resourcepack/shaderpack) e `Side` (Ambos/Cliente/Servidor — ver
   [[concepts/modside-rules]]) **editáveis** por linha. Três formas de adicionar:
   busca no CurseForge, import de modpack inteiro e upload manual de `.jar`
   (`FileId` sintético negativo). Botão **"Buscar atualizações"** checa os mods CF em
   lote e aplica as escolhidas (`ModUpdatesDialog`) — ver
   [[decisions/curseforge-update-tracking]].
3. **Overrides** — **`MudTreeView`** (árvore de pastas/arquivos) + editor
   **Monaco** (ver abaixo). **Carregamento preguiçoso**: a **raiz** é semeada em
   `Items` (carregada no init) e os **filhos diretos** de cada pasta vêm do
   `MudTreeView.ServerData` ao expandir (`ModpackImportService.ListOverrideChildren`
   — um nível, não-recursivo). _(O `ServerData(null)` automático não semeava a raiz
   de forma confiável; por isso `Items` semeia o topo.)_
   `_fileSet` é populado incrementalmente conforme as pastas abrem; mudanças
   estruturais resetam a árvore (bump no `@key`) para o `ServerData` reler. Import
   mostra um **modal de feedback bloqueante** (`ImportProgressDialog`) até terminar.
   **Três ações por item** (`ExpandOnClick=false`): o **chevron** (pasta) expande;
   clicar no **nome** seleciona e abre o arquivo no editor; o **botão à direita**
   (`BodyContent` + `stopPropagation`) **move** o item via `OverridePathDialog`
   (destino vazio = raiz). Há também **drag-and-drop**: arrastar um item e soltar
   sobre uma **pasta** move pra dentro dela, sobre um **arquivo** move pra pasta-pai,
   e na **área vazia** da árvore move pra raiz. O **destaque do alvo** (`.ovr-drop`)
   é feito por **JS client-side** (`wwwroot/js/overrides-dnd.js`, delegação no
   `document`) — em Blazor Server, fazê-lo via `@ondragenter` tinha um **delay**
   grande (round-trip por evento); o `@ondrop`/`@ondragstart` seguem no servidor (uma
   vez por arraste). Botão e DnD chamam o mesmo núcleo
   (`MoveToFolderAsync`) → `MoveOverrideAsync`/`MoveOverrideFolderAsync` (já com
   histórico/desfazer; o serviço recusa mover uma pasta pra dentro de si mesma).
4. **Novidades** — newsletter **por modpack** (CRUD direto via `ModpackNewsService`),
   em **`MudDataGrid`** (padrão de listas: busca + `MudDataGridPager`).
5. **Servidores** — entradas (nome/endereço/porta) que o launcher escreve no
   `servers.dat`; lista **editável inline** (exceção ao padrão de tabela), paginada
   (`MudPagination`).

> **Decomposição:** cada aba é um componente em `Panels/` (`DetailsPanel`,
> `ModsPanel`, `ServersPanel`, `NewsPanel`) + `OverridesPanel`. O `ModpackEditor`
> orquestra: segura `_draft`/`_mods` e passa por parâmetro (referência) ou
> `EventCallback`. **Todas as listas têm paginação.**
>
> **Origem CurseForge:** modpacks importados mostram um **banner** (verificar/atualizar
> versão) alimentado pela tabela 1:1 `ModpackImportSources` — ver
> [[decisions/curseforge-update-tracking]].

> **Troca de aba com feedback:** abas pesadas (Mods/Overrides com muitos itens)
> travam o render; a troca passa por `OnPreviewInteraction` (cancela + refaz a
> ativação sob o overlay) para a modal aparecer antes do render — ver
> [[concepts/async-feedback-overlay]].
>
> **Upload (MudBlazor 9.5):** o ativador do `MudFileUpload` usa **`CustomContent`**
> com `context.OpenFilePickerAsync()` (o `ActivatorContent` legado não é a API
> documentada e o Rider o sinaliza como inexistente).

## Duas políticas de escrita (importante)

O editor mistura **deliberadamente** dois modelos de persistência:

- **Escrita-só-ao-Guardar** para metadados, mods e servidores: tudo vive num
  rascunho destacado (`ModpackEntity` em memória) e só o botão **Guardar**
  persiste, via `ModpackImportService.SaveAsync` — que baixa os jars ainda sem
  hash com **progresso** (`IProgress<SaveProgressDto>` → `MudProgressLinear`).
- **Escrita direta em disco** para overrides: `CreateOverride`/`Write`/`Delete`/
  `Move`/`Upload` gravam na hora e alimentam o histórico/desfazer. Por isso a aba
  de overrides só fica ativa **depois do primeiro Guardar** (antes não há pasta
  do modpack no disco) e os overrides de um import ficam **pendentes** (um
  `byte[]` zip em memória) até o Guardar extraí-los.
- **Escrita direta no banco** para as **novidades**: `ModpackNewsService` faz
  CRUD imediato (conteúdo independente do rascunho), também só após o primeiro
  Guardar (a FK precisa do modpack já gravado). Cada mutação chama
  `ContentNotifier.Bump()`.

## Import + merge

`ImportModpackToDraftAsync` traz metadados + mods (com `Side` inferido pelo
server pack) + bundle de overrides. O merge é por `FileId`: mod novo é
adicionado, mod já presente tem só os campos resolvidos atualizados (preserva o
`Side`/`Target` que o admin já ajustou). Os metadados de versão do import
sobrescrevem os do rascunho; o nome só preenche se estiver vazio.

## Editor Monaco (overrides)

- Pacote **BlazorMonaco** (`serdarciplak/BlazorMonaco`) **3.4.0**, versão central
  em `Directory.Packages.props` (ver [[decisions/central-package-management]]);
  scripts de setup (3) no `App.razor`.
- Componente `StandaloneCodeEditor` (tema `vs-dark`); a linguagem do realce é
  setada por extensão via `Global.SetModelLanguage(IJSRuntime, model, lang)` ao
  selecionar o arquivo.
- **Lifecycle (bug corrigido):** o editor fica **sempre montado** (overlay cobre
  quando nada está selecionado). Antes ele era renderizado só ao selecionar um
  arquivo, então o `@ref` ainda era `null` quando `SetValue` era chamado e o
  arquivo **não abria**. Agora, se a seleção ocorre antes do init, o conteúdo
  fica pendente e é aplicado no `OnDidInit`.
- Só arquivos de **texto** (`ModpackImportService.IsTextOverride`) abrem no
  editor; binários (imagens, zips) mostram um aviso e não são editáveis.

## Newsletter por modpack

Implementada (2026-06-24) com a opção **FK opcional**: `NewsEntity.ModpackId` é
um `Guid?` — `null` = notícia **global**, preenchido = notícia **do modpack**.
Relacionamento com `OnDelete(Cascade)` (apagar o modpack apaga as suas notícias;
as globais ficam) + índice em `ModpackId`. Migration `NewsModpackFk` gerada nos
dois providers (SQLite + Postgres). A UI (`ModpackNewsService` + aba Novidades +
`NewsEditDialog`) só cria/edita notícias **do modpack**; o feed global é gerido à
parte (a fazer).

## Pendências conhecidas

- Feed/CRUD das **notícias globais** (FK nula) — ainda sem UI própria.
- Mover arquivo/pasta de override tem suporte no serviço, mas a UI ainda só expõe
  criar/editar/apagar/upload/desfazer/histórico.
- Criar instância de servidor Minecraft a partir do modpack — futuro.
