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
  - "[[decisions/auth-msal-launcher]]"
---

# TCMine-Launcher

> App desktop **Avalonia 12** (MVVM + ReactiveUI), "a Steam do TCMine". **100% dependente** do
> [[entities/tcmine-server]]: não cria instâncias locais — só consome o servidor (login, catálogo e,
> mais adiante, install/launch).

## Visão geral

`TCMine-Launcher` (namespace `TCMine_Launcher`) é o cliente desktop. **Estado atual: login +
catálogo** — o jogador entra com a conta Microsoft (**MSAL no próprio launcher**) e vê o catálogo de
modpacks publicados pelo servidor. Install/launch do jogo ainda não implementados.

## Responsabilidades / Escopo

- **Bootstrap (`Program.cs`/`App.axaml(.cs)`):** Avalonia classic desktop lifetime,
  `UsePlatformDetect`, fonte **Inter**, `UseReactiveUI` + registro de serviços no **Splat**
  (`ServerConfig`, `ApiClient`, `AuthService`, `MainWindowViewModel`); developer tools em DEBUG.
- **Serviços (`Services/`):**
    - `ServerConfig` — base URL do servidor (dependência total; dev `https://localhost:7002`).
    - `ApiClient` — HTTP contra o servidor; reusa os **DTOs `record`** do core
      ([[entities/tcmine-application]]). Só catálogo (`/api/modpacks`, `/api/modpacks/{id}`).
    - `AuthService` — login Microsoft **MSAL no cliente** (CmlLib + XboxAuthNet): `TryLoginSilentAsync`
      (cache DPAPI), `LoginAsync` (WebView2 interativo), `SignOutAsync`. Devolve uma `MSession`.
    - `AppConfig` — config embutida no build (`TcmineServerUrl`, `MicrosoftClientId`).
    - `PlayerSession` — identidade da UI (uuid/username) derivada da `MSession`.
    - `LauncherPaths` — dados em `%AppData%/TCMine`.
- **MVVM (`ViewModels/`, `Views/`):** `MainWindowViewModel` (shell/gate login↔catálogo + logout),
  `LoginViewModel` (botão "Entrar com Microsoft" + progresso), `ModpacksPageViewModel` (lista o
  catálogo); views correspondentes + `ViewLocator` (mapeia `XViewModel`→`Views.XView`).
- **Login Microsoft:** ver [[decisions/auth-msal-launcher]] — o login é **no launcher** (MSAL public
  client, redirect loopback); o servidor não participa. O Azure client id é embutido no build.

## Decisões e estado atual

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
- [ ] Detalhe do modpack + **baixar/instalar** (manifesto, jars, overrides) e montar a instância.
- [ ] **Lançar** o Minecraft (CmlLib ou equivalente).
- [ ] Integrar Velopack (auto-update) e o **build do launcher pelo servidor** (injetando
  `TcmineServerUrl` **e** `MicrosoftClientId` a partir das settings; gerar o feed Velopack).

## Referências

- Código: `TCMine-Launcher/Program.cs`, `App.axaml.cs`, `Services/`, `ViewModels/`, `Views/`
- Fontes: [[sources/2026-06-23-leitura-codigo-vivo]], [[sources/2026-06-29-launcher-login-catalogo]]
