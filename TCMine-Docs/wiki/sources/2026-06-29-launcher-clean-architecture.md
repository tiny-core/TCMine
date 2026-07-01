---
type: source
title: Launcher — Clean Architecture + Home estilo backup (2026-06-29)
tags: [source, code, launcher, clean-architecture, ui]
status: ingested
created: 2026-06-29
updated: 2026-06-29
source-type: code
origin: sessão de implementação (refactor do TCMine-Launcher)
feeds:
  - "[[decisions/launcher-clean-architecture]]"
  - "[[entities/tcmine-launcher-infrastructure]]"
  - "[[entities/tcmine-launcher]]"
---

# Launcher — Clean Architecture + Home estilo backup (2026-06-29)

Três pedidos do usuário: (1) **Home no estilo da foto** (hero + painel de perfil/ID/servidores); (2)
**não instalar imediatamente** — o card de Modpacks só **seleciona**, a instalação é o botão grande da
Home; (3) **arquitetura limpa** com os componentes alocados nos projetos da solução.

## O que foi feito

- **Refactor Clean Architecture** (ver [[decisions/launcher-clean-architecture]]): models →
  `TCMine-Domain/Launcher`; portas → `TCMine-Application/Launcher`; impls → novo projeto
  **`TCMine-Launcher.Infrastructure`**; `TCMine-Launcher` fica só com UI + composição. ViewModels
  dependem só das portas; `Program.cs` é o composition root.
- **Home (estilo da foto):** 2 colunas — hero (banner gradiente, OFICIAL, nome/versão/descrição) +
  barra "Pronto para jogar" + botão **JOGAR**; painel direito com **avatar** (iniciais + skin via
  `ImageLoader`/mc-heads), **IDENTIFICADOR** (ID + abrir pasta) e **SERVIDORES** (ping + play por
  servidor).
- **Comportamento:** clicar num modpack chama `SelectModpackAsync` (regista metadados + abre a Home),
  **sem download**; instalar/lançar é o `Play` da Home. Mods continuam a vir do **cache do servidor**
  (`/files/...`).

## Pendências

- Validação em execução (compila 0/0; falta lançar o jogo end-to-end).
- A grelha de "instaladas" saiu da Home (a foto não a tem) — virá na aba **Instâncias**.
