---
type: decision
title: Login Microsoft via MSAL no launcher
tags: [decision, auth, microsoft, launcher, msal, minecraft]
status: aceita
created: 2026-06-29
updated: 2026-06-29
deciders: [Jocian]
supersedes: ["[[decisions/server-brokered-microsoft-login]]"]
superseded-by: []
sources:
  - "[[sources/2026-06-29-launcher-login-catalogo]]"
related:
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-server]]"
  - "[[concepts/player-config-sync]]"
---

# Login Microsoft via MSAL no launcher

> O login Microsoft acontece **dentro do launcher** (MSAL + CmlLib, public client com cache DPAPI),
> não no servidor. O servidor só **valida** o token Minecraft quando precisa. Substitui a tentativa
> [[decisions/server-brokered-microsoft-login]].

## Contexto

A primeira tentativa pôs o login no servidor (auth code + PKCE + entrega via SSE). Na prática isso
exigia, no Azure: um **redirect URI Web** (acoplado a onde o site será hospedado — ainda indefinido) e
um **client secret** (cliente confidencial). O usuário já tinha um launcher (backup v1.2.0) que fazia
login com **MSAL** sem nada disso, e decidiu voltar a esse modelo.

## Decisão

**MSAL no launcher**, como no backup:

- `AuthService` (launcher) usa `JELoginHandler` (CmlLib) + `MsalClientHelper` (XboxAuthNet.Game.Msal)
  + `Microsoft.Identity.Client`. No Windows, login interativo via **WebView2** (popup embutido) e
  silencioso via **cache DPAPI** do MSAL — reentra entre execuções sem repedir credenciais.
- A cadeia Xbox→XSTS→Minecraft e o perfil são resolvidos pelo CmlLib no cliente; o launcher fica com a
  `MSession` (token + UUID + username).
- **Azure client id embutido no build** (`AppConfig.MicrosoftClientId`, via `AssemblyMetadataAttribute`
  `MicrosoftClientId` — mesmo mecanismo do `TcmineServerUrl`). Não é segredo (public client desktop).
- **Redirect = loopback** (`http://localhost`, gerido pelo MSAL) — **não precisa de hosting nem de
  redirect Web nem de client secret** no Azure.
- O servidor **não participa do login**. Quando precisa autenticar uma escrita do jogador (ex.: sync de
  configs, ver [[concepts/player-config-sync]]), o launcher manda o token Minecraft como Bearer e o
  `MinecraftAuthService` valida contra a Mojang.

## Consequências

- **Positivas:** zero dependência de hosting/redirect-web/secret; reusa o fluxo provado do backup; o
  refresh token fica no cache do MSAL (DPAPI, por utilizador Windows); vamos precisar do CmlLib de
  qualquer forma para **lançar** o jogo.
- **Negativas / custos:** o **Azure client id** passa a viver no launcher (público, aceitável); o login
  depende do **WebView2** (presente no Windows 11); o servidor deixa de ter um registo central de
  contas de jogador (não havia necessidade real — a identidade é a própria conta Minecraft).
- **Revert:** removidos `AuthEndpoints`, `LoginSessionBroker`, `MicrosoftAuthService`,
  `PlayerSessionService`, `PlayerAccountEntity` (+repo/porta), o campo client secret e as migrações
  `PlayerAccount`/`AzureClientSecret`. `MinecraftAuthService` permanece.
- **Limpeza posterior (2026-07-03):** como as migrações `PlayerAccount` foram **apagadas do histórico**
  (não convertidas em `DropTable`), bancos que as aplicaram durante os testes ficaram com uma tabela
  `PlayerAccounts` órfã. A migration `DropOrphanPlayerAccounts` (Sqlite + Postgres) faz um
  `DROP TABLE IF EXISTS "PlayerAccounts"` idempotente — remove-a onde sobrou, no-op no resto.

## Setup no Azure (mínimo)

- App registration com **Supported account types** incluindo contas pessoais
  (`signInAudience` = `AzureADandPersonalMicrosoftAccount` ou `PersonalMicrosoftAccount`) — senão o
  scope `XboxLive.signin` não resolve (AADSTS650053).
- **Authentication → Mobile and desktop applications** → redirect `http://localhost` (loopback do MSAL).
- **Allow public client flows = Yes**. **Sem client secret, sem redirect Web.**

## Referências

- Código: `TCMine-Launcher/Services/{AuthService,AppConfig}.cs`, `ViewModels/{LoginViewModel,MainWindowViewModel}.cs`.
- Substitui: [[decisions/server-brokered-microsoft-login]].
