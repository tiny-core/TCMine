---
type: source
title: Implementação do overlay bloqueante de feedback async
tags: [source, code, blazor, ux]
status: ingested
created: 2026-06-25
updated: 2026-06-25
source-type: code
origin: "Pedido do usuário + código vivo: TCMine-Server/Services/BusyService.cs, Components/Shared/BusyOverlay.razor e consumidores em Components/Pages/Admin/"
feeds:
  - "[[concepts/async-feedback-overlay]]"
  - "[[entities/tcmine-server]]"
related:
  - "[[concepts/modpack-admin-editor]]"
---

# Implementação do overlay bloqueante de feedback async

> O usuário pediu um modal não-fechável de feedback em toda operação async/banco —
> atual e futura.

## Resumo

Criado o par `BusyService` (scoped, contador de operações) + `BusyOverlay`
(`MudOverlay` não-fechável no `RootLayout`). Todas as operações async disparadas
pelo usuário no painel passaram a envolver a chamada em `Busy.RunAsync(...)`. Os
skeletons das listas (Users, Modpacks) foram removidos — o overlay cobre o load.
Registrada a convenção no `CLAUDE.md` (Parte I) para valer nas operações futuras.

## Pontos-chave

- **Escolha de design:** overlay global único reagindo a um serviço por circuito, em
  vez de um `MudDialog` por operação — evita problemas de prerender e suporta
  operações concorrentes via contador.
- **Não-fechável:** `MudOverlay` com `AutoClose` padrão (false), sem botão de
  fechar, sem ESC/backdrop.
- **Decisão de escopo do usuário:** "toda operação async" (incluindo loads),
  substituindo skeletons. Exceções por UX: refresh recorrente (`SystemStatusCard`),
  buscas internas de diálogos, fluxos com progresso dedicado (import/Save) e
  micro-leituras de seleção.
- O `try/catch`+`Snackbar` permanece na página; `RunAsync` libera no `finally`.

## O que alimentou na wiki

- Criou [[concepts/async-feedback-overlay]].
- Atualizou [[entities/tcmine-server]] (componentes + decisão datada).

## Referências

- `TCMine-Server/Services/BusyService.cs`, `Components/Shared/BusyOverlay.razor`.
- `CLAUDE.md` → Parte I, "Feedback de operações async (servidor Blazor)".
