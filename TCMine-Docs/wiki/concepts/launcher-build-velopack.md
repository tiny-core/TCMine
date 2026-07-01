---
type: concept
title: Compilação do launcher pelo servidor (Velopack)
tags: [concept, launcher, velopack, build, releases, auto-update]
status: stable
created: 2026-07-01
updated: 2026-07-01
aliases: [build do launcher, LauncherBuildService, vpk pack, feed de updates]
sources:
  - "[[sources/2026-07-01-launcher-build-velopack]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[entities/tcmine-launcher]]"
  - "[[concepts/async-feedback-overlay]]"
  - "[[concepts/server-instance-lifecycle]]"
---

# Compilação do launcher pelo servidor (Velopack)

> O próprio TCMine-Server **compila e empacota** o launcher e publica o feed Velopack em
> `tcmine-data/updates` (servido em `/updates`; o `Setup.exe` em `/download`).

## O que é

O admin abre `/admin/releases`, informa versão + notas e clica em **Compilar launcher**. O servidor então:

1. `dotnet publish` do launcher (`-c Release -r win-x64 --self-contained`), **injetando** por build:
   `-p:TcmineServerUrl=<PublicBaseUrl>` e `-p:MicrosoftClientId=<AzureClientId>` (ver
   [[entities/tcmine-launcher]]; viram `AssemblyMetadataAttribute`).
2. `vpk pack` (Velopack CLI) → gera `RELEASES`, `*-full.nupkg`, `*-Setup.exe`, `releases.<canal>.json` no
   diretório de updates.
3. Grava um `ReleaseEntity` (versão, canal, notas, arquivos).

## Por que importa para o TCMine

Fecha o ciclo "a Steam do TCMine": a URL do servidor e o client id do login são **embutidos no build**
(não hardcoded, não editáveis pelo usuário), então cada servidor produz o **seu** launcher, apontando
para si, sem configuração manual do cliente. E o auto-update do launcher passa a ter uma origem.

## Detalhes / Variações

- **`LauncherBuildService`** (singleton, [[entities/tcmine-server-infrastructure]]) — job em **segundo
  plano com progresso reconectável** (mesmo padrão do `ProvisioningCoordinator`): a página `/admin/releases`
  se inscreve no evento `Changed` e sobrevive a um refresh. Um build de cada vez. Streama a saída do
  `dotnet`/`vpk` ao vivo (coalescida por rótulo, com throttle) e captura a saída completa para o erro.
- **`ReleaseService`** (scoped) — lista o histórico de `ReleaseEntity` para a página.
- **`LauncherFeedService`** (já existente) — inspeciona `tcmine-data/updates` para a versão publicada e o
  `Setup.exe`; alimenta a Home ("Baixar"/"em breve") e o `/download`. O `vpk pack` produz exatamente os
  artefatos que ele procura (`*-full.nupkg`, `*Setup.exe`).
- **Bootstrap no launcher (obrigatório):** o `Program.Main` do launcher chama
  **`VelopackApp.Build().Run()`** como primeira instrução (pacote `Velopack`). Sem isso o `vpk pack`
  **recusa** o build ("Unable to verify VelopackApp is called") e a app instalada não se auto-gerencia.
- **Versão:** o diálogo pré-preenche o **próximo patch** a partir da última do feed (`SuggestNextVersion`);
  o usuário ajusta. Notas viram `--releaseNotes` (arquivo markdown) e o changelog do update.
- **Config:** `LauncherBuild:ProjectPath` (override do csproj), `LauncherBuild:Rid` (`win-x64`),
  `LauncherBuild:Channel` (`win`), `LauncherBuild:VpkPath`. O `vpk` é resolvido pelo PATH ou por
  `~/.dotnet/tools/vpk`.
- **Guarda de settings (2026-07-01):** compilar exige **URL pública + Azure Client Id** configurados
  (ambos embutidos no launcher em build-time). O botão fica **desabilitado** com aviso se faltar qualquer
  um, e o serviço também recusa.
- **Cross-compile Linux→Windows (validado, 2026-07-01):** o `vpk` **gera o feed de Windows a partir do
  Linux** — basta o **diretório de OS `[win]`**: `vpk [win] pack …` (o `LauncherBuildService` o adiciona
  quando `!OperatingSystem.IsWindows()`). Testado num container `sdk:10.0`: `dotnet publish -r win-x64` +
  `vpk [win] pack` geraram `Setup.exe`/`nupkg`/`RELEASES` de Windows. Isso viabiliza a **imagem Docker
  autossuficiente**.
- **Imagem Docker autossuficiente (2026-07-01):** o `TCMine-Server/Dockerfile` deixou de ser runtime-only
  e passou a **base SDK** carregando a **fonte do launcher** + **vpk** (global tool) + **JRE**, com
  `dotnet restore -r win-x64` pré-feito. Assim o container **compila o launcher em runtime** (o painel
  dispara). `ENV LauncherBuild__ProjectPath=/src/TCMine-Launcher/TCMine-Launcher.csproj` (o app roda em
  `/app`, a fonte em `/src`).

## Aplicação concreta

- `TCMine-Server.Infrastructure/Launcher/`: `LauncherBuildService`, `ReleaseService`, `LauncherFeedService`.
- `TCMine-Server/Components/Pages/Admin/Releases/`: `Releases.razor` (+ diálogo `LauncherBuildDialog`).
- `TCMine-Launcher/Program.cs`: `VelopackApp.Build().Run()`.
- Endpoints: `/updates` (estáticos, feed), `/download` (`Setup.exe`).

## Versionamento (uma versão só)

> Decisão (2026-07-02): **uma versão para tudo** — a mesma tag `v*` no GitHub define o servidor **e** o
> launcher. Mais simples que as duas faixas do backup (`server-v*`/`launcher-v*`), porque aqui o launcher é
> compilado **pelo** servidor, na versão dele.

- **Fonte da versão:** o GitHub Actions (`.github/workflows/server-image.yml`) dispara na tag `v*`,
  extrai `X.Y.Z`, builda a imagem com `--build-arg SERVER_VERSION=X.Y.Z` (→ `-p:Version` do assembly +
  `ENV SERVER_VERSION`) e publica no Docker Hub (`<user>/tcmine-server:X.Y.Z` + `:latest`). O servidor lê a
  própria versão via `AppVersion.Current` (`SERVER_VERSION` ou o assembly).
- **Self-update do servidor:** `GitHubReleaseService` consulta `/repos/tiny-core/TCMine/releases` (client
  `github` com User-Agent, cache 1h, tolerante a falha), pega a maior `v*` e compara com a versão corrente.
  Se maior → banner "atualização do servidor" na página de Releases. Atualizar = **puxar a imagem nova +
  reiniciar o container** (que traz a fonte do launcher naquela versão).
- **Launcher segue o servidor:** `LauncherBuildService.TargetVersion` = a versão do servidor;
  `NeedsBuild()` = feed publicado atrás da versão do servidor (ou inexistente). A página compila **nessa
  versão** (sem digitar) e o botão fica **desabilitado quando o feed já está na versão do servidor**.
- **Auto-build (`LauncherAutoBuildService`, IHostedService):** no **boot** e ao **salvar as settings**,
  se `NeedsBuild()` e as settings (URL/AzureId) prontas, compila o launcher em segundo plano. Assim o
  fluxo de container (parar → puxar imagem nova → subir) **já recompila o launcher** — zero-touch. O botão
  manual continua para forçar.

## Contradições / debates conhecidos

- **Precisa de SDK + fonte + vpk no ambiente** — por isso a imagem Docker é **autossuficiente** (base SDK).
  Se rodar num ambiente sem esses (ex.: SDK removido), o build falha com mensagem clara.
- **Sem assinatura de código** (o `vpk` avisa): os binários não são assinados — o SmartScreen do Windows
  pode alertar. Assinatura (`--signParams`/Azure Trusted Signing) fica como melhoria.
- **Falta o consumidor:** o launcher ainda **não checa/aplica** updates no runtime (`UpdateManager` contra
  `/updates`). O bootstrap (`VelopackApp.Build().Run()`) trata os hooks de instalação; o *check* periódico
  e o "atualizar agora" na UI do launcher são o próximo passo.

## Referências

- Fonte: [[sources/2026-07-01-launcher-build-velopack]].
- Validado ponta-a-ponta: `dotnet publish` + `vpk pack` geram o feed (`RELEASES`, `-full.nupkg`,
  `-Setup.exe`, `releases.win.json`).
