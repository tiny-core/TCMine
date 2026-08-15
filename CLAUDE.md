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
- **Nomes de métodos de teste podem ficar em português** (é a exceção).

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
- **Colunas em snake_case** (`modpack_versions`, `game_servers`, `project_slug`).
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
- **`[LibraryImport]`** exige `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` no csproj (o marshalling gerado usa
  `unsafe`). Fica contido na Infrastructure.

---

## 9. Testes

- **xUnit v3.** Nomes de método podem ser em português.
- **Lógica pura / casos de uso** → `Application.Tests` com **fakes** escritos à mão (repositório fake que devolve a
  mesma instância para as mudanças ficarem visíveis). Evite mocks quando um fake é mais claro.
- **Integração com EF** → `Infrastructure.Tests` com **SQLite in-memory**
  (`SqliteTestFactory`: uma conexão viva + `EnsureCreated`, factory servindo contextos sobre ela).
- **Regras de camada** → `Architecture.Tests` (NetArchTest). Não quebre a direção das dependências.
- **Ao corrigir um bug, escreva o teste de regressão** que o trava. Foi assim que blindamos o `UpdateVersionAsync`.

---

## 10. Comandos úteis

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
falhar com um muro de MSB3026.

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
