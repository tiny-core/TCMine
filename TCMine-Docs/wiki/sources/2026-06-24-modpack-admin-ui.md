---
type: source
title: Implementação da UI admin de modpacks
tags: [source, modpack, blazor, admin]
status: ingested
created: 2026-06-24
updated: 2026-06-24
source-type: code
origin: "código vivo — TCMine-Server/Components/Pages/Admin/Modpacks/* (sessão 2026-06-24)"
feeds:
  - "[[concepts/modpack-admin-editor]]"
  - "[[entities/tcmine-server]]"
related:
  - "[[entities/tcmine-infrastructure]]"
---

# Implementação da UI admin de modpacks

> Sessão de 2026-06-24: construída a página de criação/edição de modpacks sobre o
> `ModpackImportService` (que já existia completo no backend).

## Resumo

O backend de modpacks (`ModpackImportService`: busca/import CurseForge, cache de
jars, upload manual, CRUD/edição/move/desfazer de overrides, save com progresso)
já estava pronto. Esta sessão entregou a **camada Blazor**: lista, editor em abas
e diálogos, mais a adição do **BlazorMonaco** para edição de overrides.

## Pontos-chave

- Arquivos novos em `TCMine-Server/Components/Pages/Admin/Modpacks/`:
  `Modpacks.razor(.cs)`, `ModpackEditor.razor`, `ModpackEditor.razor.cs`,
  `ModpackEditor.Overrides.cs`, e `Dialogs/{CurseForgeSearchDialog,
  ImportModpackDialog,OverridePathDialog,OverrideHistoryDialog}.razor`.
- **BlazorMonaco 3.3.0** adicionado ao `Directory.Packages.props`, referenciado
  no `TCMine-Server.csproj`, com `@using` no `_Imports.razor` e os 3 scripts de
  setup no `App.razor`.
- API confirmada na fonte da v3.3.0: `StandaloneCodeEditor` com `SetValue`/
  `GetValue`/`GetModel`; `Global.SetModelLanguage(IJSRuntime, model, lang)`.
- Build da solução inteira: 0 erros (avisos pré-existentes só no launcher).
- **Não commitado** (regra §11 do `CLAUDE.md`).

## Decisões / gatilhos

- BlazorMonaco era **requisito explícito** do usuário (link do repo dado) — sem
  gatilho de confirmação.
- **Newsletter por modpack** pedida, mas contradiz o `NewsEntity` global
  (gatilho §5: contradição + impacto amplo) → **adiada**, a decidir com o usuário.

## O que alimentou na wiki

- Criou [[concepts/modpack-admin-editor]].
- Atualizou [[entities/tcmine-server]] (componentes + decisões + pendências).

## Referências

- Código: `TCMine-Server/Components/Pages/Admin/Modpacks/`,
  `Directory.Packages.props`, `TCMine-Server/Components/App.razor`,
  `TCMine-Server/Components/_Imports.razor`
