---
type: concept
title: Overlay bloqueante de feedback async
tags: [concept, blazor, ux, feedback, mudblazor]
status: stable
created: 2026-06-25
updated: 2026-06-25
aliases: [BusyService, BusyOverlay, feedback async, modal de carregamento, overlay bloqueante]
sources:
  - "[[sources/2026-06-25-busy-overlay]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[concepts/modpack-admin-editor]]"
---

# Overlay bloqueante de feedback async

> Toda operação async disparada pelo usuário no painel mostra um modal
> **não-fechável** enquanto roda, via um `BusyService` por circuito e um único
> `BusyOverlay` no layout raiz.

## O que é

Um par serviço + componente que centraliza o feedback de operações assíncronas:

- **`BusyService`** (`TCMine-Server/Services/BusyService.cs`, **scoped** = um por
  circuito Blazor): mantém um **contador** de operações ativas e a mensagem atual,
  e dispara um evento `OnChange`. Expõe `RunAsync(message, op)` (com e sem retorno)
  que faz begin → executa → `finally` end. O contador suporta operações
  sobrepostas/aninhadas — o overlay só some quando a última termina.
- **`BusyOverlay`** (`TCMine-Server/Components/Shared/BusyOverlay.razor`): um
  `MudOverlay` renderizado **uma única vez** no [[entities/tcmine-server]]
  (`RootLayout`, junto dos providers do MudBlazor). Assina o `OnChange` e
  re-renderiza com spinner + mensagem. É **não-fechável**: sem ESC, sem clique no
  backdrop (`AutoClose` padrão = false), sem botão de fechar.

## Por que importa para o TCMine

Dá ao usuário um sinal claro de "estou processando, espere" e **impede duplo
envio**/interação concorrente durante gravações no banco. Substitui os skeletons de
carregamento por um feedback uniforme em todo o painel, e estabelece o padrão para
**todas as operações futuras** (regra no `CLAUDE.md`, Parte I).

## Detalhes / Variações

- **Uso:** `await Busy.RunAsync("Salvando…", async () => { await Service...; });`.
  O `try/catch` + `Snackbar` fica na página — o overlay é só o feedback *durante*;
  o resultado (sucesso/erro) vai pro snackbar. Como `RunAsync` libera no `finally`,
  exceções propagam normalmente para o `catch` da página.
- **Overlay primeiro (garantido):** `RunAsync` faz `Begin` → **`await Task.Yield()`**
  → executa a operação. O yield cede o contexto para o Blazor renderizar e enviar a
  modal ao cliente **antes** de a operação começar — assim a primeira coisa visível
  é o overlay, mesmo que a operação trave a thread de render logo em seguida.
- **Troca de aba pesada:** abas com muitos itens (Mods/Overrides) travam a thread de
  render ao montar a tabela/árvore, fazendo o clique parecer morto. Solução no
  `ModpackEditor`: `MudTabs` controlado (`ActivePanelIndex`) + `OnPreviewInteraction`
  — o handler faz `args.Cancel = true` e refaz a troca dentro de `Busy.RunAsync`
  (muda `_activeTab` → `StateHasChanged` → `await Task.Yield()`), de modo que o
  overlay aparece antes do render pesado do painel.
- **Log de passos (2026-07-01):** o `BusyService` acumula um histórico de passos
  (`Steps`) além do `Message`; o `BusyOverlay` mostra a lista (concluídos com check,
  o atual com spinner) quando há mais de um. Serve a operações multi-etapa como o
  provisionamento. Coalescência: mensagens com o mesmo rótulo antes de `" — "`
  substituem a linha (o detalhe ao vivo, ex.: `%`, não infla o log).
- **Layout do overlay (2026-07-01):** spinner → **status global** (a fase atual, só o
  rótulo antes de `" — "`; o detalhe técnico fica na lista) → **lista de passos** com
  `max-height` + `overflow-y:auto` (scrollbar quando cresce) e **auto-scroll** para o
  passo atual (JS `tcmineScrollToBottom` + `ElementReference` no `OnAfterRenderAsync`).
  Larguras do `MudPaper`/`MudText` por Style inline (o CSS escopado não atinge o DOM de
  componentes MudBlazor filhos); a lista, sendo HTML próprio, usa `BusyOverlay.razor.css`.
- **Progresso ao vivo exige liberar o dispatcher (2026-07-01):** operações com
  **trabalho síncrono pesado** (ex.: provisionamento — link de centenas de arquivos)
  no dispatcher do circuito **engolem o progresso**: os `progress.Report` marcam
  `StateHasChanged`, mas o render só corre quando a operação cede a thread — e aí ela
  já terminou (overlay limpo). Sintoma: o overlay fica parado na mensagem inicial e
  fecha sem mostrar os passos. Correção no `ServerInstanceDetail`: rodar a operação em
  **`Task.Run`** (fora do dispatcher) e criar o `Progress` **no circuito** (os callbacks
  marshalam de volta ao dispatcher, agora livre para renderizar cada passo). Vale para
  qualquer operação sob overlay com etapas síncronas + progresso ao vivo; I/O async puro
  (ex.: import do `ModpackEditor`) não precisa, pois cede a thread naturalmente.
- **Padrão de recarga:** mutação + reload da lista no **mesmo** `RunAsync` (um
  helper `ReloadAsync` interno, sem overlay próprio), para não piscar o overlay
  duas vezes em sequência.
- **Onde se aplica:** loads de página, criar/editar/excluir/salvar (usuários,
  modpacks, settings, novidades, overrides), uploads, busca/add de mods.
- **Exceções deliberadas (mantêm feedback próprio):**
  - refresh recorrente em background (`SystemStatusCard`) — bloquear a cada tick
    seria absurdo;
  - buscas/listas internas de diálogos (ex.: busca CurseForge) — o usuário ainda
    interage, um overlay impediria digitar;
  - fluxos com **progresso dedicado**: import e Save do `ModpackEditor` (modais de
    progresso com etapas/contadores — ver [[concepts/modpack-admin-editor]]);
  - micro-leituras interativas (clicar num arquivo na árvore de overrides).

## Aplicação concreta

- Serviço/comp.: `TCMine-Server/Services/BusyService.cs`,
  `TCMine-Server/Components/Shared/BusyOverlay.razor`; montado no `RootLayout`;
  registrado **scoped** no `Program.cs`.
- Consumidores: `Admin/Users/Users`, `Admin/Modpacks/{Modpacks,ModpackEditor,
  ModpackEditor.News,OverridesPanel}`, `Admin/Settings`, `Admin/Dashboard`.

## Contradições / debates conhecidos

- O usuário pediu feedback em **toda** operação async; aplicamos a tudo que é
  disparado pelo usuário, com as exceções acima por motivo de UX (não regressão).

## Referências

- [[sources/2026-06-25-busy-overlay]]
- `CLAUDE.md` → Parte I, "Feedback de operações async (servidor Blazor)".
