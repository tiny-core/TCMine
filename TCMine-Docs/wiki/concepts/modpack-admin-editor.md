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
  - `Modpacks.razor` (+ `.razor.cs`) — rota `/admin/modpacks`: catálogo (lista
    em **`MudDataGrid` virtualizado** — leve com muitos modpacks). Restrito a
    `Owner,Admin`.
  - `ModpackEditor.razor` (+ `.razor.cs` + `.News.cs`) — rotas
    `/admin/modpacks/new` e `/admin/modpacks/{Id}`: o editor em abas (orquestra;
    o conteúdo de overrides vive no seu próprio componente).
  - `OverridesPanel.razor` (+ `.razor.cs`) — componente próprio da aba Overrides
    (árvore + Monaco). `OverrideTreeBuilder.cs` monta a árvore do `MudTreeView`.
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
2. **Mods** — tabela com `Target` (mod/resourcepack/shaderpack) e `Side`
   (Ambos/Cliente/Servidor — ver [[concepts/modside-rules]]) **editáveis** por
   linha. Três formas de adicionar: busca no CurseForge, import de modpack
   inteiro e upload manual de `.jar` (`FileId` sintético negativo).
3. **Overrides** — **`MudTreeView`** (árvore de pastas/arquivos) + editor
   **Monaco** (ver abaixo). Import mostra um **modal de feedback bloqueante**
   (`ImportProgressDialog`) que impede interação até terminar.
4. **Novidades** — newsletter **por modpack** (CRUD direto via `ModpackNewsService`).
5. **Servidores** — entradas (nome/endereço/porta) que o launcher escreve no
   `servers.dat`.

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
