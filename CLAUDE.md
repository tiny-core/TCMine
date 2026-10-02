# CLAUDE.md — TCMine

> Este arquivo orienta o Claude Code a trabalhar neste repositório. **Leia-o por
> inteiro antes de qualquer tarefa.** Ele descreve o que o projeto é, como está
> arquitetado, as convenções obrigatórias, as decisões de design já tomadas (e
> por quê), e a postura esperada de você como colaborador.

---

## 0. Sua postura como colaborador (leia primeiro)

Você **não é um executor cego de pedidos**. Você é um engenheiro sênior parceiro neste projeto. Especificamente:

- **Performance, limpeza e arquitetura vêm antes de "só funcionar".** Nunca entregue a primeira solução que passa;
  entregue a **melhor** solução viável. Se há uma abordagem mais performática, mais limpa ou mais bem arquitetada,
  proponha-a — mesmo que dê mais trabalho.
- **DRY é regra, não sugestão.** Antes de escrever código, verifique se a lógica já existe. Se for repetir algo, extraia
  para um método/serviço/abstração compartilhada. Duplicação silenciosa é dívida.
- **Aponte decisões erradas.** Se eu pedir algo que viola a arquitetura, cria um bug latente, fere performance, ou
  contradiz uma decisão já tomada neste documento — **diga-me antes de implementar**, explique o porquê, e proponha a
  alternativa. Não implemente errado só porque foi pedido.
- **Aponte decisões defasadas.** Se você notar código, padrão ou dependência que ficou obsoleto (uma API deprecada, um
  padrão que superamos, um TODO antigo que virou risco), sinalize e proponha a modernização.
- **Sempre proponha melhorias.** Ao terminar uma tarefa, se enxergar um ponto adjacente que poderia ficar melhor,
  mencione-o (sem implementar sem eu pedir).
- **Explique o "porquê", não só o "o quê".** Ao propor algo, justifique a decisão técnica. Eu quero entender o
  trade-off, não só receber código.
- **Na dúvida de design, pergunte antes de codar.** Uma decisão de arquitetura errada custa refactor. Se há mais de um
  caminho razoável, apresente as opções com sua recomendação e espere minha escolha.

---

## 1. O que é o TCMine

TCMine é um **ecossistema para distribuir e jogar modpacks de Minecraft**. Dois produtos, um repositório:

- **TCMine Server** (`src/server/`) — painel administrativo web (Blazor Server + MudBlazor) rodando em Linux/Docker. O
  admin cria modpacks, ingere mods do Modrinth ou CurseForge, gerencia overrides (configs), publica versões imutáveis, e
  orquestra **servidores de jogo** como containers Docker (`itzg/minecraft-server`). Serve o catálogo/manifestos e os
  arquivos (jars) para o launcher.
- **TCMine Launcher** (`src/launcher/`) — cliente desktop (WPF + BlazorWebView, ainda em construção) que instala e
  atualiza as instâncias do jogador, baixando do content store do servidor.

O fluxo central: o servidor **publica manifestos completos** de cada versão; o launcher **reconcilia** o disco do
jogador contra o manifesto (baixa o que falta, apaga o que sobrou). É um modelo **declarativo** — o manifesto descreve o
estado final desejado, e o launcher faz o disco convergir para ele.

---

## 2. Arquitetura — Clean Architecture

A dependência **sempre aponta para dentro**. Camadas de fora conhecem as de dentro, nunca o contrário. Isso é
**verificado por testes** (NetArchTest, em
`tests/TCMine.Architecture.Tests`) — se você inverter uma dependência, o build fica vermelho.

```
Domain  ←  Application (Contracts + Abstractions/portas + Casos de uso)  ←  Infrastructure  ←  Web
```

- **`TCMine.Server.Domain`** — entidades, regras de negócio, máquinas de estado. Zero dependência de framework. Pastas:
  `Modpacks`, `Servers`, `Blobs`,
  `Identity`, `Common`.
- **`TCMine.Contracts`** (shared) — DTOs e enums compartilhados entre servidor e launcher. É a camada mais "de dentro"
  que os dois lados enxergam. **Enums como
  `ModLoader`, `FileSide`, `ModpackVersionState` vivem aqui** (não no Domain), porque o launcher também os usa.
- **`TCMine.Server.Application`** — casos de uso + **portas** (interfaces em
  `Abstractions/`) que a Infrastructure implementa. Pastas: `Modpacks`,
  `Servers`, `Abstractions`, `Security`, `Common`. **Nunca referencia Infrastructure.**
- **`TCMine.Server.Infrastructure`** — implementações das portas: EF Core (`Persistence`), blob store (`Storage`),
  Docker (`Docker`), materialização de instâncias (`Instances`), ingestão/resolvers (`Ingestion`), catálogo de versões
  (`Versions`).
- **`TCMine.Server.Infrastructure.Sqlite` / `.Postgres`** — assemblies de migração separados, um por provider (dev usa
  SQLite, prod usa PostgreSQL).
- **`TCMine.Server.Web`** — Blazor Server. Páginas em `Components/Pages`, diálogos/componentes em `Components/Features`.
  Consome casos de uso via DI.
- **`TCMine.UI.Shared`** (shared RCL) — tema MudBlazor, design tokens, chips e componentes reutilizáveis entre server e
  launcher.

### Regra de ouro do registro no DI

- Registro que nomeia **classe concreta de repositório/adapter** (`ModpackRepository`,
  `FileSystemBlobStore`, `DockerServerOrchestrator`…) → vai em
  `AddTCMineInfrastructure`.
- Registro que nomeia **caso de uso** (`CreateModpack`, `MoveOverride`…) → vai em
  `AddTCMineApplication`.
- A Application **nunca** vê o nome de uma classe de Infrastructure, só a interface.

---

## 3. Stack técnica

- **.NET 10**, C# com recursos modernos (primary constructors, collection expressions, `required`, pattern matching).
- **Blazor Server** (`@rendermode InteractiveServer`) + **MudBlazor 9.7**.
- **EF Core 10**, dual provider **SQLite (dev) / PostgreSQL (prod)**.
- **BlazorMonaco** (editor de overrides).
- **SignalR** (comunicação em tempo real com o launcher/painel).
- **Docker Engine API** (orquestração de containers — HTTP sobre socket Unix/named pipe, **sem** `Docker.DotNet`).
- **Serilog** (logging), **Central Package Management** (`Directory.Packages.props`).
- Testes: **xUnit v3**, **NSubstitute** (quando útil), **Shouldly**, **NetArchTest** (regras de camada).

---

## 4. Convenções OBRIGATÓRIAS

Estas não são preferências — são regras do projeto. Segui-las sempre.

### 4.1 Idioma

- **Identificadores em inglês** (variáveis, métodos, classes, parâmetros).
- **Comentários em português (PT-BR)**, com **explicações claras do porquê** — não do óbvio. Um bom comentário explica a
  *decisão* ou o *risco*, não repete o que o código já diz. Exemplo bom: `// Detached graph: Update marca tudo
  Modified de uma vez, mas com filhos novos o resultado é imprevisível.`
- **Nomes de métodos de teste podem ficar em português** (é a exceção — e só os
  de TESTE; um helper de teste segue a regra geral).
- A base já foi varrida uma vez para cumprir isto. Duas armadilhas de quem
  repetir a operação: os `{Placeholder}` de um `[LoggerMessage]` vivem dentro de
  uma string mas são **acoplados ao nome do parâmetro** — renomear um sem o
  outro quebra a compilação com um erro que fala de template; e uma string
  interpolada mistura português que fica com identificador que muda, na mesma
  linha. Renomear com regex sobre o ficheiro inteiro estraga comentário e texto.

### 4.2 Logging

- **Sempre** via source generator: classe `partial` + métodos `partial private`
  decorados com `[LoggerMessage]`. **Nunca** chamar `logger.LogInformation/
  LogError/LogWarning` diretamente (viola **CA1848** e aloca à toa).

### 4.3 Feedback assíncrono na UI

- **Todo** processo assíncrono na UI (ingestão, upload, publicação, start/stop…)
  deve dar **feedback visual**: `MudProgressLinear`/`MudProgressCircular` e/ou botão desabilitado enquanto pendente.
  Usar componentes nativos do MudBlazor, **sem CSS custom pesado**.

### 4.4 Result pattern

- Falhas de regra de negócio retornam **`Result` / `Result<T>`**
  (`TCMine.Server.Application.Common`), **não exceções**. `.Succeeded`, `.Error`,
  `.Value`, `Result.Fail("msg")`, `Result.Success()`. Exceções são para o inesperado (Docker fora do ar, rede), e aí o
  caso de uso as captura e devolve
  `Result.Fail` com a causa real.

### 4.5 Persistência

- **GUID v7** como chave primária (`Guid.CreateVersion7()`), sortável cronologicamente. **Ordene por `Id`, não por
  `DateTimeOffset`** — o SQLite rejeita `DateTimeOffset` em `ORDER BY`.
- **Tabelas em snake_case** (`modpack_versions`, `game_servers`,
  `installation_settings`). **Colunas continuam em PascalCase** — não há
  `HasColumnName` em lugar nenhum e toda migration existente as gera assim. Este
  documento dizia "colunas em snake_case" e estava errado; renomeá-las hoje seria
  uma migration por tabela para ganhar nada. Siga o que o código faz.
- **`IDbContextFactory<TcMineDbContext>`** com **um contexto curto por operação**
  no repositório (nunca um `DbContext` scoped compartilhado — no Blazor Server isso acumularia entidades e daria
  `DbUpdateConcurrencyException`).
- Enums persistidos como **string** (`.HasConversion<string>()`), não int.
- Propriedades computadas (ex.: `HasWorld`, `IsPreRelease`) precisam de
  `builder.Ignore(...)` na configuração, senão o EF tenta criar coluna.

### 4.6 Blob store (content-addressed)

- Arquivos (jars, overrides) são armazenados por **SHA-256** (content-addressed), em layout shard
  `{sha[0:2]}/{sha[2:4]}/{sha}`. Conteúdo idêntico é deduplicado automaticamente. **Mover/copiar um arquivo não move
  bytes** — só muda o ponteiro (`Path`) do `ModpackFile`. Blobs **nunca** são apagados ao remover modpack/versão (podem
  ser compartilhados); um GC de órfãos seria tarefa separada.

### 4.7 Identidade de mod (`ProjectSlug`)

- `ModpackFile.ProjectSlug` é a **identidade estável** do mod (project_id do Modrinth), independente da versão do
  arquivo. É por ele que `UpsertFile`
  **substitui** (não acumula) quando um mod é atualizado — dois `.jar` do mesmo mod na pasta `mods/` crashariam o jogo.
  Overrides usam slug sintético
  `override:{path}`.

---

## 5. Modelo de domínio (essencial)

### Modpack → ModpackVersion → ModpackFile

- **`Modpack`** — dono do catálogo. Fixa **`MinecraftVersion` e `Loader`**
  (imutáveis após criação — mods não migram entre versões de MC/loader; travar isso evita crash).
- **`ModpackVersion`** — uma versão publicável. Fixa a **`LoaderVersion`** (essa sim pode subir entre versões). Máquina
  de estados:

  ```
  Draft → Resolving → Ready → Archived
              ↓ Failed
  Ready/Archived são IMUTÁVEIS.
  ```
    - `Draft`: editável (mods, overrides, número, RAM).
    - `Resolving`: job em background baixando/hasheando.
    - `Ready`: publicado, imutável. `Archived`: aposentado (some de novas instalações, mas quem já fixou continua
      rodando).
    - Métodos: `MarkResolving`, `MarkReady`, `MarkFailed`, `ReturnToDraft`,
      `Archive`, `Restore`, `UpsertFile`. **Regra: uma Draft por vez por modpack.**
- **`ModpackFile`** — `Path`, `Sha256`, `SizeBytes`, `Side` (`Both`/`ClientOnly`/
  `ServerOnly`), `Origin`, `ProjectSlug`, `OriginReference` (o **version id** do Modrinth — usado para detectar
  atualizações comparando com a versão mais recente, sem baixar).

### GameServer (instância de jogo)

- Fixa `ModpackVersionId` **no servidor, não no modpack** (permite rollout gradual e rollback por re-apontamento).
  `ConnectAddress`, `Status`,
  `ContainerId`, `MemoryMb`, `MaxPlayers`, `RconSecret` (**required, NUNCA exposto em DTO nem log** — quem tem a senha
  RCON controla a máquina do jogo),
  `WorldInitializedAt`/`HasWorld` (seam do backup).
- Só pode ser criado apontando para versão **`Ready` e de canal release** (não alpha). Alpha = `Version` com sufixo `-`
  (pré-release SemVer, `IsPreRelease`).

---

## 6. Orquestração de servidores (Docker)

- **`IServerOrchestrator`**: `EnsureCreatedAsync`, `StartAsync`, `StopAsync`,
  `GetStatusAsync`, `RemoveAsync`. Implementado por `DockerServerOrchestrator`.
- **O container é a fonte da verdade do status, não a coluna.** A coluna `Status`
  é cache; sincronize com `GetStatusAsync` (que inspeciona o Docker) ao carregar, e reconcilie no arranque. Um container
  `unless-stopped` sobrevive a reinícios do TCMine — não há "attach", só reconsulta pelo `ContainerId`.
- **Transporte Docker**: `HttpClient` com `SocketsHttpHandler.ConnectCallback`
  sobre socket Unix (`/var/run/docker.sock`) ou named pipe (Windows,
  `npipe://./pipe/docker_engine`). Config em `DockerOptions`. A `BaseAddress`
  `http://localhost` é fictícia. **Se ver `localhost:80` num erro, o ConnectCallback não está sendo usado.**
- **`IInstanceMaterializer`**: escreve a pasta da instância (`{root}/{serverId}`)
  a partir de uma `ModpackVersion`. Monta como volume `/data` no container itzg.
    - **`mods/` usa hardlink** do blob store (jars read-only, onde estão os bytes); **o resto copia** (configs podem ser
      reescritos em runtime; hardlink corromperia o blob compartilhado).
    - **Preserva `world/` e dados do jogador.** Usa manifesto local (`.tcmine-manifest.json`) para saber o que
      gerenciou; só remove o que ele mesmo escreveu. **Trocar versão reescreve mods sem apagar o mundo.**
- **Backup de mundo**: trocar a versão de um servidor **com mundo** tira um snapshot automático antes de
  re-apontar (`WorldBackupReason.BeforeVersionChange`). Se o backup falhar, a troca é cancelada — é isso que a
  torna reversível. Backup e restauração **exigem servidor parado**: copiar o mundo com o jogo escrevendo produz
  um .zip íntegro que não abre. Snapshot é um `.zip` por vez em `{root}/backups/{serverId}`, fora da pasta da
  instância (que o materializador reescreve).
- **Backup a quente**: com o servidor NO AR, o backup faz `save-off` → `save-all flush` → copia → `save-on`,
  este último em `finally` **sempre**. Deixar o autosave desligado é pior que não ter backup: o servidor roda
  sem persistir e a próxima queda leva tudo. Se o `save-on` falhar, a exceção sobe — é a única falha do módulo
  que exige ação imediata. **Restaurar continua exigindo servidor parado** (os arquivos são substituídos, e
  nenhum comando impede o jogo de reabri-los).
- **RCON via `docker exec rcon-cli`**, não pela porta 25575. Abrir a porta exporia um canal de controle total na
  rede do host e exigiria recriar containers; por dentro, o `rcon-cli` da imagem itzg já lê a senha do ambiente —
  o segredo nunca sai do container.

---

## 7. Sincronização (launcher)

- **`ManifestDiffer.Plan`** (função **pura**, testável sem I/O) compara o manifesto da versão-alvo com o estado do disco
  e produz um `SyncPlan`
  (`ToDownload`, `ToMaterialize`, `ToDelete`).
- **Deleção é implícita no diff**: um arquivo no disco que não está no manifesto entra em `ToDelete`. Remover um
  override numa versão nova = ele some do manifesto = o launcher apaga no update. **Não há registros de deleção.**
- **GUARD CRÍTICO** (fixado em teste): o `localFiles` passado ao differ deve ser **apenas o conjunto gerenciado** (via
  manifesto local). `saves/`,
  `screenshots/`, `options.txt` etc. **nunca** podem entrar no cálculo de
  `ToDelete` — o applier do launcher jamais deve passar dados do jogador ao differ. O primeiro update apagaria os
  mundos.

### 7.0 Instância, canal e atualização

- **A instância tem identidade PRÓPRIA** (`InstanceKey`), e não é mais o par
  (modpack, versão). A diferença decide duas coisas: atualizar mantendo o mundo
  — mesma instância, versão nova — e ter duas instalações do mesmo pack, cada
  uma no seu mundo. Com a chave no par, todo update criava pasta nova e o mundo
  ficava para trás.
- **O identificador É o nome da pasta.** Não é economia: as instalações
  anteriores foram adotadas pelo nome que já tinham, e por isso a mudança não
  precisou de migração — ninguém renomeou pasta com mundo dentro. `ListAsync`
  tira a chave do diretório, não do manifesto.
- **Quem instala decide o alvo**: chave existente atualiza, `null` cria. O
  catálogo passa `null`; a tela de atualização passa a chave da instância.
- **Uma instância existente só anda para a FRENTE.** Escolher versão é coisa da
  instalação, nunca da atualização: descer para uma versão antiga por cima de
  uma instalação parte os mundos jogados — os mods que somem levam consigo os
  blocos e itens que registaram.
- **Backup antes, e a falha dele CANCELA a atualização.** É a ordem que torna a
  operação reversível, a mesma regra do servidor. Automático quando há mundo, e
  não um checkbox: backup que depende de lembrar de marcar não existe no dia em
  que importa. O `.zip` vai para `{raiz}/backups/{instância}/`, fora da pasta que
  o instalador reescreve. A retenção guarda as **cinco** mais recentes por
  instância (`WorldBackupRetention`, no Core porque decidir o que se perde merece
  teste): a mais nova nunca expira — a poda corre logo depois de criar a cópia
  que a atualização exigiu — e `keep: 0` significa ILIMITADO, não "apague tudo".
- **Canal** (`ReleaseChannel`) sai do NÚMERO da versão, por SemVer: o que tem
  hífen é alpha. Não é campo gravado — guardá-lo criaria um segundo lugar para a
  verdade, e o dia em que discordassem seria um pack a atualizar para o canal
  errado. A regra vive em `ReleaseChannels.Of`, partilhada com o `IsPreRelease`
  do domínio.
- **Um canal não vê o outro**, e é isso que faz dele um canal. A verificação de
  atualização agrupa por **(modpack, canal)**. Uma instância alpha não se
  duplica: cada cópia deixada para trás seria uma instalação presa numa alpha que
  ninguém mais atualiza.

### 7.1 O casco do launcher (WPF hospedando Blazor)

O launcher usa **as mesmas telas do painel**: Blazor + MudBlazor + `TCMine.UI.Shared`.
Nada de Avalonia nem de XAML de tela — a janela é a única coisa em WPF.

```
TCMine.Launcher.UI    (RCL, net10.0)          ← TODAS as telas. Portável.
TCMine.Launcher.App   (WPF, net10.0-windows…) ← a janela, o WebView2, o P/Invoke.
```

- **Por que a RCL separada**: no dia de rodar em Linux, portar é escrever um host
  novo — as páginas ficam intactas. `Launcher_UI_e_portavel` e
  `Launcher_UI_nao_conhece_a_infraestrutura` (NetArchTest) travam isso.
- **O que a tela precisa do sistema entra por porta**: `IWindowChrome`
  (arrastar/minimizar/maximizar/fechar) e `LauncherAppInfo` (título e versão)
  vivem em `Launcher.UI/Abstractions` e são implementados/fornecidos pelo host.
- **Estado**: `LauncherShellState`, singleton, no lugar do ViewModel da janela.
  Em Blazor Hybrid há um circuito só, e o estado precisa sobreviver à navegação.
  A lógica que merece teste continua em classes puras (ver `NavMatch`,
  `ManifestDiffer`).
- **Tema**: `TcMineTheme.Default` e `<TcMineTokens/>`, os mesmos do painel. O
  launcher é sempre escuro (`data-theme="dark"` no `index.html`), sem alternador.
- **CSS**: o que é global e vale para os dois produtos mora em
  `_content/TCMine.UI.Shared/tcmine.css`; o que é do casco, em
  `_content/TCMine.Launcher.UI/launcher.css`; o resto é `.razor.css` isolado.
  **Nada vem da rede** — o launcher tem de abrir sem internet.
- **Protocolo**: `Protocol.Current` está em **2**, e o mínimo também. Subiu
  quando o canal de versões acrescentou um parâmetro a dois métodos do hub — o
  SignalR resolve por nome E aridade, então um launcher de protocolo 1
  conectaria e falharia na primeira consulta com um erro que não fala de versão.
  Recusar no handshake diz-lhe para atualizar. **Mudança de aridade no hub é
  mudança quebrada**; campo opcional novo num DTO não é.
- **Pareamento**: `ServerPairing` (no Core) é o primeiro caso de uso a rodar.
  `ResumeAsync` lê o `tcmine.json` e confirma o handshake; `PairAsync` valida o
  endereço digitado, faz o handshake e só então grava. Duas regras não são
  cosméticas: o endereço é recusado **antes** do handshake se não for HTTPS (ou
  loopback), porque o id_token da Microsoft trafega nessa conexão; e uma falha de
  rede **não** desfaz o pareamento, senão o jogador redigita o endereço a cada
  oscilação de sinal. O `ShellLayout` faz o arranque uma vez por sessão e manda
  para `/pair` só quando não há configuração nenhuma.
- **`ResumeAsync` adota o que o servidor responde AGORA** (client id do Azure e
  nome), e grava só quando muda. Sem isso o pareamento congelava no dia em que
  aconteceu: o admin corrigia o registo do Azure no painel e quem já tinha pareado
  continuava a abrir o navegador com o id antigo — sintoma real, e sem nada na
  tela que apontasse para o `tcmine.json`. Um valor **em branco não substitui** o
  gravado: servidor sem login configurado é estado transitório, e adotá-lo daria
  um ficheiro que o próprio launcher recusa a carregar depois.
- **O client id do Azure vem do SERVIDOR, e mora na tela de configurações dele**
  (`InstallationSettings.AzureClientId`), não em appsettings — registar a app no
  Entra ID acontece depois do deploy. `Server:AzureClientId` sobrevive como
  semente: o handshake usa o do banco e cai no do arquivo quando aquele está
  vazio. Não é segredo (public client + PKCE), então não é cifrado e volta para a
  tela preenchido.
- **Login**: `SignIn` (no Core) junta dois passos que só valem juntos — provar a
  conta à Microsoft (`IMinecraftAuthenticator`) e trocar essa prova por sessão no
  servidor
  (`ILauncherSessionApi` → `POST /api/v1/auth/minecraft`). O token do Minecraft
  **não** é guardado: vale uma vez, e o que vale daí em diante é o cookie que o
  servidor devolve — o mesmo do painel. Esse cookie vive num `CookieContainer`
  **singleton** partilhado pelos clientes HTTP; um pote por cliente faria o
  jogador entrar e ser anônimo no pedido seguinte.
- **O servidor fora do ar NÃO prende o jogador.** O arranque já mandou para
  `/login` nesse caso, e o login não tinha como ajudar: ele pede a conta
  Microsoft, entrar troca a prova por sessão no TCMine, e o TCMine é justamente
  o que não está lá. Offline fica-se onde se está, e o que precisa de rede
  aparece desligado no trilho com o motivo — Modpacks e Novidades saem, Jogar e
  Instâncias ficam, porque leem o disco.
- **O resultado do login mora no `LauncherShellState`, não na página.** A
  tentativa que mais importa é a do arranque (credencial guardada), e ela
  acontece no `ShellLayout`. Guardando o resultado num campo da tela de login,
  uma sessão expirada levava a um login em branco: o jogador tinha de clicar para
  descobrir o que já se sabia.
- **A autenticação tem dois degraus, e a fronteira não é onde parece.** Só o
  primeiro é do Windows: `IMicrosoftTokenProvider` (MSAL, em
  `Infrastructure.Windows`). A cadeia seguinte — Xbox Live → XSTS → Minecraft
  Services — é HTTPS comum, vive em `MinecraftAuthenticator` na infraestrutura
  **portável**, e é testada inteira com um `FakeHttpHandler`, sem app do Azure.
  Enfiar tudo no projeto do Windows compilaria e faria o port para Linux
  reescrever o que não tem nada de Windows.
- **O cache é do MSAL, e isso está decidido.** Guardar o refresh token à mão não
  fecha: um public client **não tem API** para reinjetar um refresh token vindo
  de fora, então o login silencioso passa obrigatoriamente pelo cache dele
  (`Extensions.Msal`, DPAPI no Windows). Havia um `ICredentialStore` prometendo o
  contrário; foi apagado. Um ficheiro de cache **por client id** — partilhá-lo
  faria sair de um servidor encerrar a sessão no outro.
- **Navegador do sistema, nunca WebView embutida** (`WithUseEmbeddedWebView(false)`).
  É a diferença entre o jogador ver a barra de endereço da Microsoft e escrever a
  palavra-passe numa janela que qualquer um podia ter desenhado — e é o que fixa
  o redirect URI em `http://localhost`, o mesmo que a tela de configurações manda
  registar.
- **A tradução dos erros do MSAL mora no Core** (`MicrosoftSignInFailures`) e tem
  teste, porque é decisão de produto e não detalhe da biblioteca: fechar o
  navegador é `Cancelled` e a tela cala-se; credencial expirada vira
  `NoStoredCredentials` no arranque; app mal registada nomeia o administrador,
  senão o jogador tenta para sempre contra algo que nunca vai aceitar. O que
  atravessa a fronteira é um código de erro, que é `string` — a regra proíbe
  **depender** do pacote, não conhecer o vocabulário dele.
- **Em Debug, `EnvironmentMinecraftAuthenticator` só assume o lugar quando
  `TCMINE_DEV_MINECRAFT_TOKEN` existe.** Registá-lo incondicionalmente esconderia
  o MSAL de quem o está a desenvolver — a build de Debug nunca abriria o
  navegador. Não é bypass: o token é REAL e o servidor continua a verificá-lo com
  a Mojang; o arquivo nem é compilado em release.
- **Catálogo**: `IServerConnection` (porta) esconde o SignalR de tudo acima dela,
  e `SignalRServerConnection` guarda UMA conexão para a aplicação inteira — uma
  por tela faria o servidor ver o mesmo jogador como vários. O canal é aberto
  **sob demanda** pelo `LoadCatalog`, e não no login: ligar no login deixaria a
  tela sem saída quando a conexão caísse depois, porque o botão de "tentar de
  novo" não teria o que religar. Sair fecha o canal (ver `SignIn.SignOutAsync`).
- **`LauncherCatalogContractTests` sobe a aplicação num socket REAL (Kestrel em
  porta efêmera), e não no TestServer.** É o único jeito de exercer o cliente do
  launcher como ele é: ele monta o próprio `HttpClient` e o próprio
  `HubConnection`, e não há onde injetar o handler em memória sem furar o desenho
  para o teste ver. Foi esse teste que pegou o `[.. ]` no hub.
- **Instalação**: `InstallModpackVersion` é instalar E atualizar — não existe
  caminho separado para "primeira vez", porque um diff contra uma instância vazia
  já É a instalação completa. O **guard crítico** vive nele: o `localFiles`
  entregue ao `ManifestDiffer` vem do `InstanceManifest` que gravamos, **nunca**
  de uma varredura da pasta. Uma varredura acharia `saves/`, `screenshots/` e
  `options.txt`, que não estão no manifesto do pack e entrariam em `ToDelete` —
  o primeiro update apagaria os mundos. Há teste de unidade e teste de contra em
  `LauncherInstallContractTests` para isso.
- **Manifesto local ilegível é tratado como AUSENTE.** A consequência é
  deliberada: o diff vê uma instância vazia, baixa tudo de novo e não apaga nada,
  porque sem conjunto gerenciado não há o que apagar. Perder disco é aceitável;
  perder o mundo do jogador não.
- **Hardlink só em `mods/`** (`InstanceLayout.CanHardLink`). Ligar um config
  corromperia o blob COMPARTILHADO na primeira vez que o jogo o reescrevesse, e a
  corrupção viajaria para toda instância que usasse o mesmo arquivo. O hardlink em
  si é P/Invoke e vive em `TCMine.Launcher.Infrastructure.Windows`, atrás de
  `IFileLinker`; sem ele o store copia, que é só mais disco.
- **`TCMine.Launcher.Infrastructure.Windows`** é o único lugar de P/Invoke e do
  MSAL. `Launcher_Infrastructure_e_portavel` trava a fronteira, e ela não precisou
  mudar para o MSAL entrar: a infraestrutura portável não fala com ele, fala com
  a porta. Registe por `AddWindowsLauncherInfrastructure(raiz)`, **depois** de
  `AddLauncherInfrastructure` — as duas portas têm implementação portável que
  recusa, e aqui o último registo vence.
- **O broker do Windows (WAM) está ligado** — `WithBroker`, com o HWND vindo de
  `IParentWindowHandle`, que o host implementa. Sem pai, o diálogo abre ATRÁS do
  launcher e parece que o login travou. Sem broker disponível, o MSAL cai
  sozinho para o navegador do sistema, e é por isso que o redirect de loopback
  continua registado e a tela de configurações manda registar os DOIS URIs.
- **O TFM de `Infrastructure.Windows` é `net10.0-windows` seco, e está certo.**
  Este documento já afirmou que o broker exigia `net10.0-windows10.0.19041.0`,
  por analogia com o WebView2 do host (§8). **Não exige**: com os dois TFMs o
  NuGet resolve os mesmos assets, incluindo o `NativeInterop` que traz o runtime
  do WAM. Foi medido no `project.assets.json`, não deduzido — e a analogia com o
  WebView2 é justamente o tipo de raciocínio que produz uma afirmação errada com
  cara de óbvia.
- **Abrir o jogo é do CmlLib**, atrás de `IGameLauncher`, na infraestrutura
  PORTÁVEL. A regra de camada deixou de o proibir ali e isso foi correção, não
  concessão: ele é multiplataforma, ao contrário do `Microsoft.Win32`, do
  `System.Windows` e do MSAL com broker — o próprio csproj sempre o listou aqui.
  O isolamento que interessa é a porta: trocar de motor é reescrever uma classe.
- **NeoForge não tem instalador no CmlLib**, e é o loader padrão dos packs
  modernos. O oficial é um programa Java e nós já gerimos um JRE: corre headless
  contra a raiz partilhada e deixa uma entrada em `versions/`. Os dois pipes dele
  são drenados em PARALELO — não drenar enche o buffer e pendura o processo sem
  erro nenhum.
- **Layout do jogo**: a pasta da instância é o *game dir*; `libraries/`,
  `versions/` e `assets/` apontam para `{raiz}/minecraft`. Dez packs partilham
  centenas de megabytes. O `MinecraftPath` não tem conceito de game dir — o
  construtor de dois argumentos só move os assets —, mas todas as propriedades
  são settable, então o layout monta-se.
- **`java.exe`, não `javaw.exe`.** O javaw é do subsistema gráfico e não escreve
  em stdout: com ele, mostrar o log do jogo seria impossível e todo crash viraria
  "fechou sozinho".
- **Que Java usar vem da VERSÃO, não de palpite** (`IJavaRequirementSource` lê o
  `javaVersion` do JSON; disco primeiro, Mojang depois). O `JavaRequirement` é só
  o plano B — e já falhou: o Minecraft trocou "1.y.z" por "ano.release", o parse
  recusou "26.2" e o fallback estava numa constante envelhecida. O jogo morria
  com "Could not create the Java Virtual Machine".
- **A identidade do jogador vem do MINECRAFT, não do servidor TCMine**
  (`IPlayerProfileSource`). Vinha do nosso servidor por conveniência, e por isso
  tê-lo fora do ar impedia jogar com internet e Microsoft a responder. Sem rede,
  o último perfil guardado (nome e UUID, NUNCA token) abre em modo offline.
- **O token do Minecraft é readquirido a cada abertura** por `TrySilentAsync`, e
  a conta é verificada ANTES do Java: descobrir a sessão expirada depois de
  cinquenta megabytes seria fazer esperar para só então pedir login.
- **Atualização do próprio launcher**: o servidor serve
  `/updates/launcher/{canal}/` a partir de `LauncherUpdates:RootPath` (derivado
  de `Storage:RootPath`), e o launcher consome por Velopack. O canal vem do
  PROTOCOLO, não da versão do produto. **Só funciona numa build empacotada pelo
  `vpk`**: a partir do código-fonte não há instalação para substituir e a
  biblioteca sai em silêncio — o que é o certo, senão ela reiniciar-se-ia no meio
  de uma depuração.
  Publicar é manual, na sua máquina Windows (`dotnet publish` + `vpk pack`, ver
  `docs/RELEASE.md`), e não um workflow: o launcher saiu do GitHub Actions de
  propósito (§12) — compilar/empacotar um WPF só faz sentido onde ele vai
  rodar. Depois é **copiar os ficheiros para a pasta do servidor à mão**: a
  máquina onde você empacota não devia ter credencial de escrita na que serve
  jogadores.
- **Rodar**: `dotnet run --project src/launcher/TCMine.Launcher.App`. Exige o
  runtime do WebView2 (Evergreen, já presente em Win10/11 atualizados).

---

## 8. Aprendizados que custaram bugs (não repita)

- **`UpdateVersionAsync` deve marcar arquivos existentes como `Modified`, não
  `Unchanged`.** Marcar `Unchanged` faz o EF ignorar edições in-place (mover override, renomear) silenciosamente. Há
  teste de regressão para isso.
- **`db.Update()` em grafo destacado não cascateia deleção de filhos removidos da coleção.** Ao substituir (ex.:
  `UpsertFile`), delete a linha antiga explicitamente via `RemoveFileAsync`.
- **Migrations**: a base de design-time (`tcmine-design.db`) **não** é a base que a app abre (`data/tcmine.db`). Aplicar
  migration numa não toca na outra. Rode
  `dotnet ef database update` apontando para a base certa (ou use o auto-migrate em Development). Em dev, o `RootPath`
  de instâncias é resolvido para **absoluto** (`Path.GetFullPath`) porque o bind mount do Docker exige.
- **Monaco + Blazor Web App**: a **enhanced navigation** desfaz o DOM que o Monaco monta e quebra o editor. Navegue para
  a página do editor com
  `forceLoad: true` (ou `data-enhance-nav="false"` em links).
- **MudBlazor 9.7**: para customizar linha de árvore, o par é `ItemTemplate` (no
  `MudTreeView`) → `BodyContent` (no `MudTreeViewItem`); o `Context` do
  `ItemTemplate` é `ITreeItemData<T>` (interface). O `MudTreeView` não reconstrói ao reatribuir `Items` — force com
  `@key` que muda a cada rebuild.
- **Identidade dentro de um Hub vem de `Context.User`, nunca de `IHttpContextAccessor`.** O accessor responde
  conforme o transporte: no WebSocket a requisição de upgrade continua viva e o contexto aparece; em long polling
  ela já terminou e o `HttpContext` foi reciclado, então o usuário some e toda checagem de papel vira "servidor não
  encontrado". Como o SignalR cai para long polling sozinho atrás de proxy, o bug atingiria só *alguns* jogadores.
  O principal é depositado no `UserPrincipalHolder` pelo `HubIdentityFilter`; `MainHubIdentidadeTests` trava os
  dois transportes.
- **`dotnet test` não roda esta solução no SDK do .NET 10** (o caminho VSTest foi removido e o xUnit v3 usa o
  Microsoft.Testing.Platform). Use `scripts/tc test`, que executa o `.exe` de cada suíte por `dotnet run`.
- **Método de Hub NUNCA devolve `[.. algo]` com alvo `IReadOnlyList<T>`.** A
  expressão de coleção materializa o tipo interno sintetizado pelo compilador
  (`<>z__ReadOnlyList`), e o MessagePack não o serializa: a chamada morre em
  runtime **derrubando a conexão**, e o cliente recebe "Failed to serialize".
  Compila, passa nos testes que falam JSON, e quebra só no launcher — que é o
  único que usa MessagePack. `GetModpacksAsync` e `GetServersAsync` estavam
  assim desde sempre. Devolva `ToArray()`.
- **Nada de inicializador estático que leia `Default` num `JsonSerializerContext`.**
  O `TcMineJsonContext` declarava `public static new readonly JsonSerializerOptions
  Options = new(...) { TypeInfoResolver = Default }`. Esse inicializador roda durante
  a construção da classe e força a criação do `Default` ANTES de o gerador ter
  inicializado o campo de options dele — o contexto padrão nasce sem
  `TypeInfoResolver` e fica assim em cache. Resultado: **qualquer**
  `TcMineJsonContext.Default.X` estoura com *"metadata … was not provided by
  TypeInfoResolver of type '<null>'"*, o que quebrava o handshake e a gravação do
  `tcmine.json` — o caminho inteiro do launcher. Compilava, e nenhum teste via,
  porque os dois lados usam o mesmo tipo. A correção é adiar (`Lazy<T>`).
  `HandshakeWireFormatTests` trava isso com uma resposta LITERAL do servidor:
  serializar e desserializar com o mesmo contexto passa mesmo quando o formato do
  outro lado é outro.
- **Janela sem moldura: NUNCA `WindowStyle="None"` junto de `WindowChrome`.** É a
  receita pré-`WindowChrome` e ela quebra o maximizar — a janela cresce ~8px para
  cada lado além do monitor e a barra de estado desaparece atrás da barra de
  tarefas (medido: rect `-7,-7` com 3454x1454 num ecrã de 3440x1440). Com o
  estilo padrão o Windows continua dono do enquadramento, e o `WindowChrome`
  (`CaptionHeight=0`) só estende a área de cliente sobre a moldura.
  `CaptionHeight` maior que zero também não serve: entregaria a faixa do topo ao
  gestor de janelas e os botões desenhados em HTML ficariam mortos, porque
  `IsHitTestVisibleInChrome` só vale para elementos WPF.
- **O TFM do host precisa da versão do SDK do Windows** (`net10.0-windows10.0.19041.0`,
  não `net10.0-windows`). O controle WPF do WebView2 renderiza por composição e
  chama as projeções WinRT; com o TFM seco o build passa e a janela morre no
  primeiro quadro com `FileNotFoundException: Microsoft.Windows.SDK.NET`.
- **O launcher não faz mais parte do CI** (§12): `ci.yml` builda
  `TCMine.Server.slnx`, que não lista nenhum projeto de `/src/launcher/`. O
  `EnableWindowsTargeting` que existia em `TCMine.Launcher.App` e em
  `TCMine.Launcher.Infrastructure.Windows` só servia para deixar o agente ubuntu
  compilar (não rodar) esses TFMs Windows — sem CI tocando neles, a propriedade
  virou configuração morta e foi removida dos dois csproj. Build e testes do
  launcher (`TCMine.slnx`, que continua listando `/Launcher/`) são trabalho da
  IDE, na sua máquina Windows, antes de commitar.
- **`[LibraryImport]`** exige `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` no csproj (o marshalling gerado usa
  `unsafe`). Fica contido na Infrastructure do servidor e no `Launcher.App`.
- **O Xbox devolve `DisplayClaims.xui[].uhs` em minúsculas**, e são os dois
  únicos campos assim numa resposta toda em PascalCase. O contexto source-gen
  **não** é case-insensitive: sem `[JsonPropertyName]` neles, o user hash volta
  nulo, o login morre no ÚLTIMO salto e o erro aponta para o Minecraft, que não
  tem nada com isso. Quem pegou foi o teste da cadeia, no primeiro arranque dele.
- **No `Infrastructure.Windows`, `LogLevel` é ambíguo.** O MSAL declara o dele, e
  ele não é o do `Microsoft.Extensions.Logging` — sem
  `using LogLevel = Microsoft.Extensions.Logging.LogLevel;` todo `[LoggerMessage]`
  do projeto deixa de gerar método, com erro que fala de partial e não de
  ambiguidade.
- **Config gravada não se atualiza sozinha.** O `tcmine.json` congelava o client
  id do Azure no dia do pareamento (ver §7.1). A regra geral: tudo o que o
  servidor descreve sobre si mesmo tem de ser reabsorvido no handshake seguinte,
  senão uma correção no painel nunca alcança quem já pareou.
- **`v@algo.Coisa` no Razor sai LITERAL.** É a heurística de endereço de e-mail:
  `letra@letra` não vira expressão, e não há erro de compilação a avisar. Use
  `v@(algo.Coisa)`.
- **Um registo em falta no DI não é bug de lógica — é uma peça que ninguém
  ligou.** Os testes montam os casos de uso à mão com fakes e nunca pedem nada
  ao contentor; 764 deles passaram sobre uma tela que rebentava ao abrir.
  `DependencyInjectionTests` monta a coleção real com `ValidateOnBuild` e
  `ValidateScopes` e resolve tudo. Ao acrescentar uma dependência, é ele que
  avisa.
- **`Progress<T>` num teste é uma corrida.** Ele posta no contexto de
  sincronização, então a asserção corre antes da callback: passa sozinho e falha
  com a suíte cheia — ou seja, nunca verificou nada. Use o
  `ProgressoSincrono<T>` dos fakes.
- **`SqliteConnection.ClearAllPools()` é GLOBAL ao processo.** Estava no
  `Dispose` da fábrica de testes e limpava o pool de fábricas que outros testes
  ainda usavam em paralelo; a vítima levava `ObjectDisposedException` numa
  consulta correta, uma vez em cada dez execuções. `Pooling=False` na connection
  string resolve o mesmo problema sem tocar em ninguém.
- **O `BlazorWebView` é `IAsyncDisposable`, não `IDisposable`.** Esquecê-lo
  compila e parece completo — e o processo sobrevive ao fecho da janela. O
  `OnExit` também não pode ser `async void`: o WPF não espera por ele e o
  `Dispose` depois do primeiro await pode nunca correr.
- **O Xbox devolve `xui`/`uhs` em minúsculas** (ver acima) e o **MessagePack
  recusa expressão de coleção** (ver acima): as duas armadilhas repetem-se em
  cada tipo novo que atravessa o hub. Método de hub devolve `ToArray()`, e
  contrato novo ganha teste no socket REAL — os testes que falam JSON não veem.

---

## 9. Testes

- **xUnit v3.** Nomes de método podem ser em português.
- **Lógica pura / casos de uso** → `Application.Tests` com **fakes** escritos à mão (repositório fake que devolve a
  mesma instância para as mudanças ficarem visíveis). Evite mocks quando um fake é mais claro.
- **Integração com EF** → `Infrastructure.Tests` com **SQLite in-memory**
  (`SqliteTestFactory`: uma conexão viva + `EnsureCreated`, factory servindo contextos sobre ela).
- **Regras de camada** → `Architecture.Tests` (NetArchTest). Não quebre a direção das dependências.
- **Limites de coluna** → `PostgresColumnLimitsTests`, contra um PostgreSQL de
  verdade. O SQLite **não aplica** `varchar(n)`: aceita qualquer texto e ignora o
  limite declarado. Foi assim que uma coluna curta demais passou pela suíte
  inteira e só apareceu ao importar um pack real em produção. Sem a variável
  `TCMINE_TEST_POSTGRES` esses testes **se pulam**, então rodar a suíte na sua
  máquina continua não exigindo banco. Para exercê-los de verdade:

  ```bash
  docker run -d --name pgtest -e POSTGRES_USER=tcmine -e POSTGRES_PASSWORD=teste     -p 15432:5432 postgres:17-alpine
  export TCMINE_TEST_POSTGRES="Host=localhost;Port=15432;Username=tcmine;Password=teste;Database=postgres"
  ./scripts/tc test '*PostgresColumnLimits*'
  ```

  No CI a variável está definida e eles rodam sempre.
- **Ao corrigir um bug, escreva o teste de regressão** que o trava. Foi assim que blindamos o `UpdateVersionAsync`.

---

## 10. Comandos úteis

> **Sob WSL, o `dotnet` do PATH é o `dotnet.exe` do Windows.** Duas consequências
> que custam tempo: caminhos absolutos de Linux não servem (use relativos à
> raiz), e o `dotnet ef` falha com "A compatible .NET SDK was not found" porque o
> msbuild filho resolve para o `C:\Program Files\dotnet`, que só tem o SDK 9. O
> SDK 10 está na instalação do utilizador, então:
>
> ```bash
> cmd.exe /c "set DOTNET_ROOT=C:\Users\<user>\.dotnet&& \
>   set PATH=C:\Users\<user>\.dotnet;%PATH%&& dotnet ef migrations add <Nome> ..."
> ```
>
> O `scripts/tc` também está com CRLF no disco e não executa direto sob WSL
> (`env: 'bash\r'`). Contorno: `tr -d '\r' < scripts/tc > scripts/.tc-lf && bash
> scripts/.tc-lf <cmd>` — a cópia tem de ficar em `scripts/`, porque o `ROOT` sai
> do `BASH_SOURCE`.

```bash
# Build / testes
dotnet build
dotnet test                     # roda as 4 suítes

# Migrations (uma por provider)
dotnet ef migrations add <Nome> \
  --project src/server/TCMine.Server.Infrastructure.Sqlite \
  --startup-project src/server/TCMine.Server.Infrastructure.Sqlite \
  --context TcMineDbContext

# Aplicar na base da app (de dentro da pasta do Web)
cd src/server/TCMine.Server.Web
TCMINE_DESIGN_CONNECTION="Data Source=data/tcmine.db" \
  dotnet ef database update --project ../TCMine.Server.Infrastructure.Sqlite \
  --startup-project ../TCMine.Server.Infrastructure.Sqlite --context TcMineDbContext
```

---

## 11. Fluxo de trabalho comigo

- Trabalhamos em **fatias pequenas e testáveis**, com passos incrementais.
- Prefiro entender cada mudança antes de avançar. Explique o que vai fazer e por quê, especialmente em decisões de
  arquitetura.
- Quando uma mudança toca vários arquivos (ex.: mover um campo de camada), faça-a **de dentro para fora** (Domain →
  Application → Infrastructure → Web) e deixe o compilador guiar os consumidores.
- **Antes de dar por concluído**: rode os testes, verifique se não repetiu código, e me diga se enxergou algo adjacente
  que valha melhorar.

---

## 12. Economia de contexto (leia antes de verificar qualquer coisa)

Uma sessão de agente gasta a maior parte do orçamento **relendo saída que não
mudou**: build verde, testes verdes, HTML de página. As regras abaixo existem
para cortar isso.

### 12.1 Use `scripts/tc`, nunca `dotnet` cru

| Em vez de | Use | Por quê |
|---|---|---|
| `dotnet build` | `./scripts/tc build` | devolve só erros/avisos + `BUILD OK` |
| `dotnet test` | `./scripts/tc test` | devolve só falhas + placar |
| ambos | `./scripts/tc check` | build + testes numa chamada |
| abrir sqlite à mão | `./scripts/tc db state` | versões, arquivos e pendências em 5 linhas |
| abrir o browser | `./scripts/tc smoke /rota` | o Blazor pré-renderiza no SSR: o texto da página vem no GET |

`tc build` mata o `TCMine.Server.Web.exe` antes — era ele que fazia o build
falhar com um muro de MSB3026 — e **espera** o processo sair, porque `taskkill`
volta assim que pede a morte, não quando os handles são liberados.

Ele também compila com `--no-incremental`, e isso não é preciosismo: com os
projetos atualizados o compilador nem roda, e **nenhum diagnóstico é reemitido**.
Um erro de analisador aparecia uma vez e depois era perdoado para sempre, com o
`tc` respondendo `BUILD OK` sobre código que o CI (checkout limpo) reprovaria.
Custa cinco segundos.

### 12.2 Delegue a verificação ao subagente `verify`

Para confirmar que uma fatia ficou de pé, chame o agente `verify` em vez de
rodar build/teste na conversa principal. A saída longa morre no contexto dele;
volta só o veredito.

### 12.3 Browser: último recurso

Dirigir a UI por `javascript_tool` é o caminho mais caro que existe — cada
clique é uma ida-e-volta com resposta e raciocínio. Regras:

- Prefira `tc smoke`. Ele já prova que a página renderizou e mostra o texto.
- Se precisar mesmo do browser, **agrupe tudo numa chamada só**: navegar,
  esperar, clicar e devolver as asserções num único `JSON.stringify`.
- Nunca sonde em laço (`n0`, depois `n1`, depois `n2`). Um `await sleep()`
  dentro da mesma chamada custa zero contexto; uma segunda chamada custa tudo.

### 12.4 Edição

- Não releia um arquivo inteiro para trocar três linhas: `Grep` com contexto
  localiza, `Edit` troca.
- Não releia depois de editar para "conferir" — o `Edit` teria falhado.
- Mudou uma porta (`IModpackRepository`, `IBlobStore`, `IUpstreamPackSource`)?
  Os fakes de teste herdam de `tests/.../Fakes/Fake*Base.cs`. Acrescente o
  membro **só na base**; nenhum teste precisa mudar.

---

## 13. Nuvem de itens (TC Cloud Storage)

Guarda itens de jogadores fora do mundo para o mod `tccloud` (NeoForge). Plano completo e
decisões em `docs/CLOUD-STORAGE.md` — ler antes de mexer em qualquer coisa de `Cloud`.

- **Isolamento por dono:** `CloudVault` tem `OwnerId`; um `GameServer` só liga a uma nuvem do
  MESMO dono. Toda consulta da API do mod deriva o `CloudVaultId` da CHAVE do servidor, nunca de
  um campo do corpo. Instance admin vê tudo no painel; a API do mod nunca.
- **Chave do servidor = segredo como o `RconSecret`:** gerada pelo TCMine, injetada como variável
  de ambiente (`TCMINE_CLOUD_KEY`) no container itzg, guardada só como hash SHA-256. Nunca em DTO,
  log ou tela (a tela mostra só o prefixo). Rotacionar = gerar nova + recriar container.
- **Só servidores orquestrados pelo TCMine** e com `ONLINE_MODE=true` recebem chave.
- **O ledger é append-only e é a verdade.** `cloud_balances` é derivado e atualizado na MESMA
  transação do ledger. Correção do admin = nova linha no ledger (Source=Admin, motivo obrigatório),
  nunca UPDATE direto em saldo.
- **Lote idempotente:** índice único (VaultId, PlayerUuid, Epoch, Seq). Reenvio do mesmo lote
  devolve "já aplicado", não aplica duas vezes. Lote de época velha → quarentena, nunca aplicado
  automaticamente. Saldo nunca fica negativo: o lote inteiro vai para a quarentena.
- **Concorrência sem SELECT FOR UPDATE** (o SQLite não tem): o `CloudLease` tem token de
  concorrência (`Version`); aplicar lote = transação que lê o lease, valida época/seq, aplica e
  incrementa `Version`. `DbUpdateConcurrencyException` → o mod reenvia.
- **O TCMine nunca decodifica item.** Guarda `EncodedItem` (blob opaco) + `ItemId` + nome de
  exibição que o servidor de jogo mandou.
- **Restaurar backup de mundo com nuvem ligada abre um `CloudRollbackIncident`** (prévia de
  estorno) ANTES de religar o servidor. Estorno = linhas compensatórias no ledger.
- **Backup a quente com nuvem ligada:** depois do `save-all flush`, rodar `tccloud checkpoint` pelo
  RCON ANTES de copiar — senão o zip sai com o diário do mod atrasado em relação aos chunks.
- **"Operações em dúvida" nunca são devolvidas automaticamente.** Depois de um crash o mod não sabe
  se o mundo gravou o item; devolver às cegas duplica. Só o dono decide, pelo painel.
