---
type: entity
title: TCMine-Launcher
tags: [entity, tcmine, avalonia, launcher, desktop]
status: wip
created: 2026-06-23
updated: 2026-06-29
aliases: [TCMine-Launcher, launcher, desktop]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
  - "[[sources/2026-06-29-launcher-login-catalogo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-design]]"
  - "[[concepts/design-tokens]]"
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/launcher-install-launch]]"
  - "[[decisions/auth-msal-launcher]]"
  - "[[decisions/launcher-clean-architecture]]"
  - "[[entities/tcmine-launcher-infrastructure]]"
---

# TCMine-Launcher

> App desktop **Avalonia 12** (MVVM + ReactiveUI), "a Steam do TCMine". **100% dependente** do
> [[entities/tcmine-server]]: não cria instâncias locais — só consome o servidor (login, catálogo e,
> mais adiante, install/launch).

## Visão geral

`TCMine-Launcher` (namespace `TCMine_Launcher`) é o cliente desktop. **Estado atual: login + catálogo
+ instalar/lançar** — o jogador entra com a conta Microsoft (**MSAL no próprio launcher**), vê o
catálogo de modpacks publicados, **instala e lança** o modpack oficial (NeoForge), com página Jogar e
Definições (RAM/Java). Ver [[concepts/launcher-install-launch]].

## Responsabilidades / Escopo

- **Clean Architecture (2026-06-29):** o launcher é só **apresentação + composição** — os serviços/models
  vivem nas camadas próprias: models em [[entities/tcmine-domain]] (`Launcher/`), portas em
  [[entities/tcmine-application]] (`Launcher/`), implementações em
  [[entities/tcmine-launcher-infrastructure]]. Ver [[decisions/launcher-clean-architecture]]. Os
  ViewModels dependem **só das portas**; o `Program.cs` é o composition root (Splat).
- **MVVM (`ViewModels/`, `Views/`):** `MainWindowViewModel` (+ partial `.Play.cs`: instâncias/launch),
  `HomePageViewModel` (Jogar), `ModpacksPageViewModel`, `SettingsPageViewModel`, `PlaceholderPageViewModel`;
  views correspondentes + `ViewLocator` (mapeia `XViewModel`→`Views.XView`) + `Behaviors/ImageLoader`
  (skin via mc-heads).
- **Login Microsoft:** ver [[decisions/auth-msal-launcher]] — no launcher (MSAL public client, loopback);
  o servidor não participa. O Azure client id é embutido no build.

## Decisões e estado atual

- **[2026-07-01]** **Badges de indisponibilidade + servidores via SSE.** Quando o servidor avisa (SSE)
  que o conteúdo mudou, a shell (`MainWindowViewModel`) agora, além de recarregar catálogo/ativo:
  - `ReconcileAvailabilityAsync()` cruza **todas as instâncias instaladas** com `/api/modpacks` e marca
    `InstalledModpack.ModpackMissing` nas que já não constam (removidas ou **despublicadas** — o manifesto
    `/api/modpacks/{id}` dá **404** nesse caso, agora distinguido de "servidor offline");
  - `RefreshActiveAsync()` trata o **404 do manifesto** do ativo como "modpack removido" (antes só fazia
    `return` silencioso) e, no caminho feliz, aplica os **servidores frescos**.
  - `InstalledModpack` ganhou `INotifyPropertyChanged` só para as **flags de runtime** (`ModpackMissing`,
    `AutoJoinServerMissing`, `HasAvailabilityWarning`, `AvailabilityMessage` — não persistidas), para os
    **badges** reagirem ao vivo. Badge `Border.badge.warn` na **Home** (hero) e na **lista de Instâncias**.
  - A **lista de servidores do ativo** (Home) já se reconstrói via SSE (`RefreshActiveAsync` →
    `Home.NotifyActiveChanged`) — cobre add/remove de servidor do modpack. Ver [[concepts/sse-content-sync]].
- **[2026-06-29]** **Clean Architecture** (projeto de infra dedicado `TCMine-Launcher.Infrastructure`) —
  ver [[decisions/launcher-clean-architecture]]. **Home redesenhada** no estilo do backup (hero + painel
  de perfil/ID/servidores). Clicar num modpack **só seleciona** (não instala); instalar/lançar é o botão
  da Home. Mods do **cache do servidor** (`/files`).
- **[2026-06-29]** **Login Microsoft via MSAL no launcher** (não no servidor) — ver
  [[decisions/auth-msal-launcher]], que **substituiu** a tentativa server-brokered
  ([[decisions/server-brokered-microsoft-login]]). Sem hosting/redirect-web/secret; token no cache DPAPI
  do MSAL. O `MicrosoftClientId` é embutido no build.
- **[2026-06-29]** Launcher **dependente do servidor** para o conteúdo: sem instâncias locais; catálogo
  lido de `/api/modpacks` (ver [[concepts/modpack-mods-locais]]).
- **[2026-06-29]** Reusa os DTOs do core (referência a [[entities/tcmine-application]]) em vez de
  duplicar contratos — evita mismatch de enum (`ModLoader`) e mantém um só contrato de fio.
- **[2026-06-29]** **URL do servidor injetada no build** (não hardcoded): `AppConfig` lê um
  `AssemblyMetadataAttribute` `TcmineServerUrl` (em produção o servidor compila o launcher e injeta a
  URL/IP via `-p:TcmineServerUrl=…`; em dev vem de `Client.props` ou do fallback localhost no
  `ServerConfig`). Mesmo padrão do backup.
- **[2026-06-29]** **Tema do `ColorTokens` re-introduzido**: `Theme/AvaloniaTheme.ApplyTheme` injeta os
  tokens de [[entities/tcmine-design]] como recursos Avalonia (`{Nome}Color`/`{Nome}Brush`), chamado no
  `App.OnFrameworkInitializationCompleted`. Estilos em `Themes/Styles/` (Buttons/Cards/Text) seguindo o
  **design do backup**, mas com as cores **sempre** dos tokens (`{DynamicResource}`), nunca hexes
  literais. Ver [[concepts/design-tokens]].
- **[2026-06-23]** Auto-update planejado via **Velopack** (feed estático em `{PublicBaseUrl}/updates`).

## Relações

- Consome [[entities/tcmine-server]] (login, catálogo/manifesto/jars). Compartilha DTOs do core
  ([[entities/tcmine-application]]). Usaria [[entities/tcmine-design]] via `AvaloniaTheme` (pendente).

## Pontos em aberto

- [x] Login Microsoft (MSAL no launcher) + reentrada silenciosa (2026-06-29).
- [x] Catálogo de modpacks (lista) (2026-06-29).
- [x] Tema `ColorTokens`/`AvaloniaTheme` re-introduzido + estilos no design do backup (2026-06-29).
- [x] URL do servidor injetada no build (`AppConfig`/`TcmineServerUrl`), não hardcoded (2026-06-29).
- [~] **Replicar a interface do backup** — **shell feito** (2026-06-29): janela borderless
  (`WindowDecorations="None"` + `WindowChrome` arredondado), `TitleBar` custom (logo/min/fechar +
  arrasto), **sidebar** de 64px com navegação por ícones (Jogar/Instâncias/Modpacks/Novidades + chip de
  conta/Definições), área de página com `TransitioningContentControl` (CrossFade) e **barra de estado**
  (ponto de ligação ao servidor + versão). Login no estilo do backup (card + botão Microsoft). Cores
  **sempre** do `ColorTokens` (aliases semânticos do backup emitidos pelo `AvaloniaTheme`). Falta: as
  **páginas** de features ainda não construídas (Jogar/Instâncias/Novidades/Definições estão como
  placeholder "em breve") entram conforme as features.
- [x] **Instalar + lançar** modpack NeoForge (manifesto → mods/cache → overrides → CmlLib → launch),
  página Jogar e Definições (RAM/Java) (2026-06-29). Ver [[concepts/launcher-install-launch]].
- [x] **Aba Instâncias** (lista + deletar/exportar/importar + editar RAM + abrir pastas
  shaders/texturas), **auto-join por servidor** na Home (o botão marca; JOGAR entra nele) e **janelas**
  do footer (Eventos/registo + Memória) (2026-06-29).
- [x] **Aba Novidades** (feed `/api/news`: globais + de modpacks, com badge de origem; recarrega via
  SSE) (2026-06-29).
- [ ] Loaders além de NeoForge; **sync de configs** ([[concepts/player-config-sync]], falta `GET` no servidor).
- [x] **Build do launcher pelo servidor** (2026-07-01) — `/admin/releases` compila (`dotnet publish` +
  `vpk pack`) injetando `TcmineServerUrl`/`MicrosoftClientId` das settings e gera o feed Velopack; o launcher
  chama `VelopackApp.Build().Run()` no `Main`. Ver [[concepts/launcher-build-velopack]].
- [x] **Consumir o auto-update no launcher** (2026-07-02) — porta `IUpdateService` + impl `UpdateService`
  (Velopack `UpdateManager` contra `/updates`); o shell checa no boot (`CheckUpdateAsync`) e mostra um
  **banner "Atualização disponível: vX — Atualizar agora"** que baixa, aplica e reinicia. Guarda
  `IsInstalled` (dev não checa). Ver [[concepts/launcher-build-velopack]].

## Referências

- Código: `TCMine-Launcher/Program.cs`, `App.axaml.cs`, `Services/`, `ViewModels/`, `Views/`
- Fontes: [[sources/2026-06-23-leitura-codigo-vivo]], [[sources/2026-06-29-launcher-login-catalogo]]
