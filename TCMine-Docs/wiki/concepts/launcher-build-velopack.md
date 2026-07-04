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
- **Imagem Docker autossuficiente p/ compilar (2026-07-01, revisto em 02):** o `TCMine-Server/Dockerfile`
  é **base SDK** com **vpk** (global tool) + **JRE**. **Não** embute a fonte do launcher — ela é baixada do
  GitHub por build (ver Versionamento). Assim o container compila o launcher em runtime, e a imagem fica
  leve e desacoplada do código do launcher.

## Aplicação concreta

- `TCMine-Server.Infrastructure/Launcher/`: `LauncherBuildService`, `ReleaseService`, `LauncherFeedService`.
- `TCMine-Server/Components/Pages/Admin/Releases/`: `Releases.razor` (+ diálogo `LauncherBuildDialog`).
- `TCMine-Launcher/Program.cs`: `VelopackApp.Build().Run()`.
- Endpoints: `/updates` (estáticos, feed), `/download` (`Setup.exe`).

## Versionamento (duas faixas independentes)

> Decisão (2026-07-02): **duas versões independentes** — tags `server-v*` (a imagem) e `launcher-v*` (o
> código do launcher). Reverte a tentativa de "uma versão só" (que acoplava demais): com uma versão só, uma
> mudança só no launcher forçava rebuild+restart da imagem, e uma mudança só no servidor gerava um "update"
> falso para os players. As duas faixas (como no backup) resolvem os dois casos.

- **Servidor (`server-v*`):** o GitHub Actions (`.github/workflows/server-image.yml`) dispara na tag
  `server-v*`, extrai `X.Y.Z`, builda a imagem com `--build-arg SERVER_VERSION` (→ `-p:Version` do assembly
  + `ENV SERVER_VERSION`) e publica no Docker Hub. `GitHubReleaseService` (cache 1h) compara a maior
  `server-v*` com a versão corrente (`AppVersion`) → **banner "atualize o servidor"** (puxar imagem + restart).
- **Launcher (`launcher-v*`):** **não dispara build de imagem**. É só a versão do código do launcher. O
  servidor **em execução** pega a maior `launcher-v*` do GitHub, **baixa a fonte dessa tag** (tarball
  `github.com/{repo}/archive/refs/tags/{tag}.tar.gz`, extraído com `System.Formats.Tar`), compila (injeta
  URL/AzureId) e publica o feed **nessa versão**. `NeedsBuild` = feed publicado atrás da última `launcher-v*`.
- **Desacoplamento (o ponto-chave):** como a fonte do launcher é **baixada por build** (não embutida na
  imagem), uma nova `launcher-v*` é recompilada pelo servidor rodando — **sem rebuild de imagem nem restart**.
  E `server-v*` não mexe na versão do launcher → **nada de update falso** para os players.
- **Auto-build (`LauncherAutoBuildService`, IHostedService):** no **boot**, ao **salvar settings** e num
  **poll de 1h**, se a última `launcher-v*` > feed e as settings prontas, compila em segundo plano. Assim
  uma release de launcher chega aos players sozinha, com o container de pé. O botão manual continua para forçar.

## Consumidor no launcher (auto-update)

Fecha o ciclo: o launcher **consome** o feed que o servidor gera.

- Porta `IUpdateService` ([[entities/tcmine-application]]) + impl `UpdateService`
  ([[entities/tcmine-launcher-infrastructure]]) via **Velopack `UpdateManager`** apontando para
  `{servidor}/updates`. Canal `win` (default no Windows) casa com o pack (`-c win`).
- O shell (`MainWindowViewModel`) checa no boot (`CheckUpdateAsync`); se há versão nova **e** a app está
  **instalada** (`IsInstalled` — dev não checa), mostra um **banner "Atualização disponível: vX"** com
  **"Atualizar agora"** → `DownloadAndApplyAsync` (progresso %) → **aplica e reinicia**.
- Requer o bootstrap `VelopackApp.Build().Run()` no `Main` (já presente).

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
