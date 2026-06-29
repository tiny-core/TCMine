---
type: decision
title: Login Microsoft orquestrado pelo servidor
tags: [decision, auth, microsoft, launcher, oauth, minecraft]
status: substituída
created: 2026-06-29
updated: 2026-06-29
deciders: [Jocian]
supersedes: []
superseded-by: ["[[decisions/auth-msal-launcher]]"]
sources:
  - "[[sources/2026-06-29-launcher-login-catalogo]]"
related:
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[concepts/secrets-data-protection]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/player-config-sync]]"
  - "[[concepts/sse-content-sync]]"
---

# Login Microsoft orquestrado pelo servidor

> ⚠️ **SUBSTITUÍDA em 2026-06-29** por [[decisions/auth-msal-launcher]] — implementada e depois
> revertida. Motivos: o fluxo server-side exige registar um **redirect Web** no Azure (acoplado à
> hospedagem, ainda indefinida) e um **client secret** (cliente confidencial); o usuário preferiu
> voltar ao **MSAL no launcher** (public client, loopback), que dispensa hosting/redirect-web/secret.
> O código abaixo descreve o que existiu; **nada dele está mais na base** (revert limpo).

> O launcher não faz o login Microsoft sozinho: ele inicia o fluxo, abre o navegador, e o
> **servidor** corre toda a cadeia OAuth→Minecraft e empurra o resultado de volta pela "live link"
> (SSE). O launcher guarda só uma sessão TCMine opaca; o refresh token Microsoft fica no servidor.

## Contexto

O launcher do backup (v1.2.0) fazia o login **dentro do cliente** com MSAL + WebView2, embutindo o
Azure client id via `Client.props`. O novo launcher é **100% dependente do [[entities/tcmine-server]]**
(não cria instâncias locais, só baixa do servidor — ver [[concepts/modpack-mods-locais]]). Coerente
com isso, e com a decisão de manter segredos server-side ([[concepts/curseforge-proxy]],
[[concepts/secrets-data-protection]]), o login Microsoft passa a ser **responsabilidade do servidor**.

Restrição do usuário: **sem código manual** (não é Device Code). O jogador clica "Entrar", loga no
navegador, e o resultado volta sozinho para o launcher.

## Decisão

**Authorization Code Flow com PKCE, intermediado pelo servidor, com entrega via SSE direcionada.**

1. Launcher → `POST /api/auth/microsoft/start`: o servidor cria um `loginId`, gera o par PKCE e
   devolve a **URL de autorização da Microsoft** (montada com o Azure client/tenant id das settings
   cifradas — ver `ServerSettingsService`).
2. Launcher abre essa URL no **navegador do sistema** e subscreve `GET /api/auth/microsoft/wait/{loginId}`
   (SSE) — a "live link" **direcionada** a aquele login.
3. O jogador loga na Microsoft; o navegador é redirecionado a `GET /auth/microsoft/callback`.
4. O callback corre a cadeia completa no servidor: troca o code (PKCE) → tokens MS → **Xbox Live →
   XSTS → `login_with_xbox` → perfil** (`MicrosoftAuthService`). Persiste a conta
   (`PlayerAccountEntity`) com o **refresh token MS cifrado** (Data Protection, protector
   `TCMine.PlayerAuth.v1`) e emite uma **sessão TCMine** opaca (só o hash fica no banco).
5. O servidor empurra `{ sessionToken, uuid, username, minecraftAccessToken, expiresAt }` pelo SSE; o
   navegador mostra "pode voltar ao launcher".
6. Reentradas: `POST /api/auth/session/refresh` (Bearer da sessão) usa o refresh token guardado para
   obter um novo token Minecraft — **sem novo login**.

**Cliente confidencial**: como a troca do code é server-side, o servidor usa um **client secret**
guardado cifrado (Data Protection, junto do client/tenant id em `ServerSettings`) — é o fluxo
canônico de web-app e o equivalente "confidencial" do antigo MSAL public-client do launcher. PKCE é
mantido como defesa adicional. Tenant default `consumers` (contas pessoais, requisito do Minecraft).

## Consequências

- **Positivas:** segredos Azure e refresh token MS nunca saem do servidor; o launcher fica fino (só
  uma sessão opaca, revogável por logout); sem WebView2/MSAL no cliente; reusa a infra de "live link"
  e de Data Protection já existentes. A sessão guardada no launcher é cifrada com DPAPI (Windows).
- **Negativas / custos:** o servidor passa a **guardar tokens por jogador** (mais superfície de
  segredo); o login depende do servidor estar de pé; exige **registar o redirect URI**
  `{PublicBaseUrl}/auth/microsoft/callback` no Azure (plataforma Web/pública, contas pessoais). A
  entrega por SSE precisa de um canal **direcionado** (não o broadcast do `/events`) — daí o
  `LoginSessionBroker` keyed por `loginId`.
- O `MinecraftAuthService` (validação de token contra a Mojang, usado em
  [[concepts/player-config-sync]]) continua válido e complementar ao novo `MicrosoftAuthService`
  (obtenção do token).

## Setup no Azure (requisitos — senão o login falha)

O App Registration ("TCMine Launcher") precisa de:

- **Supported account types com contas pessoais** — `signInAudience` =
  `AzureADandPersonalMicrosoftAccount` (ou `PersonalMicrosoftAccount`). O Xbox Live só existe para
  apps que suportam contas pessoais; um app **org-only** (single-tenant) faz o scope `XboxLive.signin`
  não resolver e o Azure cair no Microsoft Graph → erro **AADSTS650053** ("scope ... doesn't exist on
  the resource 00000003-0000-0000-c000-000000000000").
- **Tenant = `consumers`** (ou o campo `AzureTenantId` **vazio**, que o `MicrosoftAuthService` trata
  como `consumers`). **Nunca** um GUID de organização — também dispara o 650053.
- **Client secret** (Certificates & secrets) → preenchido no painel admin (`/admin/settings`, cifrado).
  Como a troca do code é server-side, o app é **confidencial** (não public-client/PKCE-only).
- **Redirect URI** na plataforma **Web**: `{PublicBaseUrl}/auth/microsoft/callback`. **Não precisa
  de hosting definido para começar** — registre `https://localhost:7002/auth/microsoft/callback` para
  dev (o Azure aceita localhost) e **adicione** a URL de produção depois (pode ter várias, editáveis a
  qualquer momento). Diferente do MSAL antigo, que era public-client com redirect **loopback** e por
  isso não exigia redirect web.

O scope enviado (`XboxLive.signin offline_access`) é o mesmo das libs de Minecraft (PrismLauncher,
minecraft-launcher-lib) — o erro acima é **configuração do Azure**, não do código.

## Alternativas consideradas

- **MSAL embutido no launcher (modelo do backup):** rejeitado — colocaria o Azure id no cliente e
  contraria "login pelo servidor".
- **OAuth Device Code:** rejeitado pelo usuário — exige o jogador digitar um código.
- **Auth code com loopback (redirect para `http://localhost` no launcher):** dispensável — a entrega
  via SSE reaproveita a live link e evita abrir um servidor HTTP local no launcher.
- **Refresh token no launcher (DPAPI) em vez do servidor:** considerado; o usuário preferiu
  centralizar no servidor.

## Referências

- Código servidor: `TCMine-Infrastructure/Minecraft/MicrosoftAuthService.cs`,
  `TCMine-Infrastructure/Identity/PlayerSessionService.cs`,
  `TCMine-Server/Services/LoginSessionBroker.cs`, `TCMine-Server/Endpoints/AuthEndpoints.cs`,
  `TCMine-Domain/Entities/PlayerAccountEntity.cs`.
- Código launcher: `TCMine-Launcher/Services/{AuthService,ApiClient,SessionStore}.cs`.
- Fonte: [[sources/2026-06-29-launcher-login-catalogo]].
