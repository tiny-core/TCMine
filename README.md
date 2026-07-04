# TCMine

> **A "Steam" de um servidor Minecraft com modpacks.** Um launcher desktop de um clique
> (instala loader + mods, faz login na conta Microsoft, entra no servidor) apoiado por um
> servidor web que hospeda o catálogo de modpacks, o painel de administração e — via Docker —
> orquestra as próprias instâncias de servidor Minecraft.

O TCMine é uma solução **.NET 10** em **Clean Architecture** dividida em dois produtos que
partilham um núcleo comum:

- **TCMine-Server** — backend web (ASP.NET Core: Minimal API + Blazor Server) com painel admin.
  Serve o catálogo de modpacks, faz proxy do CurseForge, entrega os jars/manifests ao launcher,
  compila e publica o próprio launcher (auto-update Velopack) e provisiona servidores Minecraft
  em containers.
- **TCMine-Launcher** — app desktop (Avalonia, MVVM/ReactiveUI). Login Microsoft, catálogo de
  modpacks, instalar/atualizar/jogar com um clique, sync das configs do jogador entre PCs.

---

## Índice

- [Do que se trata](#do-que-se-trata)
- [Tecnologias](#tecnologias)
- [Arquitetura e projetos](#arquitetura-e-projetos)
- [Documentação](#documentação)
- [Rodar em produção (Docker)](#rodar-em-produção-docker) ← **tutorial**
- [Desenvolvimento local](#desenvolvimento-local)
- [Estrutura do repositório](#estrutura-do-repositório)

---

## Do que se trata

O jogador abre o launcher, faz login com a conta Microsoft e vê o catálogo de modpacks oficiais.
Ao clicar em **Jogar**, o launcher instala o loader (NeoForge), baixa os mods **do próprio
servidor** (nunca direto do CurseForge), aplica os overrides do modpack, escreve a lista de
servidores e lança o Minecraft — entrando direto no servidor. As configs do jogador
(keybinds, opções de vídeo/áudio, shaders, waypoints e cache de mapa dos servidores)
**sincronizam entre PCs** de forma incremental.

Do outro lado, o administrador usa o painel web para montar modpacks (busca/import do CurseForge,
edição de mods e overrides), publicar notícias, **subir servidores Minecraft** em containers
dedicados e **compilar/publicar o launcher** (o servidor hospeda o feed de auto-update).

## Tecnologias

| Área | Stack |
|------|-------|
| Plataforma | .NET 10, C# |
| Backend | ASP.NET Core — Minimal API + **Blazor Server** |
| UI admin | **MudBlazor** |
| Launcher desktop | **Avalonia** (WinExe), MVVM + **ReactiveUI** |
| Persistência | **EF Core** dual-provider — **SQLite** (padrão) ou **PostgreSQL** |
| Login Minecraft | **MSAL** + **CmlLib.Core** (Microsoft/Xbox no launcher) |
| Lançar o jogo | **CmlLib.Core** (instala NeoForge + assets, arranca o Minecraft) |
| Auto-update launcher | **Velopack** (o servidor hospeda o feed em `/updates`) |
| Mods | **CurseForge** via proxy do servidor (`/v1/*`), a `x-api-key` nunca sai do servidor |
| Servidores Minecraft | **Docker-out-of-Docker (DooD)** — um container por instância |
| Push de mudanças | **SSE** (`/events`) para o launcher |
| Segredos em repouso | **Data Protection** (token CF cifrado no banco) |
| Design system | `ColorTokens` compartilhado (CSS, MudBlazor e Avalonia) |
| Empacotamento NuGet | **Central Package Management** (`Directory.Packages.props`) |

## Arquitetura e projetos

Clean Architecture: as dependências apontam **para dentro** (Domain não conhece ninguém;
Infrastructure implementa as portas do core). Servidor e launcher têm **infra própria**, sem
acoplamento cruzado, mas partilham Domain/Application.

| Projeto | Papel |
|---------|-------|
| `TCMine-Domain` | Entidades, enums e regras puras de domínio (sem EF/ASP.NET) |
| `TCMine-Application` | Portas (interfaces), contratos (DTOs `record`) e lógica pura de modpack |
| `TCMine-Server.Infrastructure` | EF Core (SQLite/Postgres), CurseForge, filesystem, Minecraft, servidores |
| `TCMine-Launcher.Infrastructure` | CmlLib, HTTP, filesystem do launcher (implementa as portas do core) |
| `TCMine-Design` | Design system compartilhado (`ColorTokens`) — fonte única de cor |
| `TCMine-Server` | ASP.NET Core: backend do launcher + painel admin (Blazor/MudBlazor) |
| `TCMine-Launcher` | App Avalonia (só UI + composição): login, catálogo, jogar |
| `TCMine-IconGenerator` | Console SkiaSharp que gera ícones/favicon/og-image |

## Documentação

A memória de longo prazo do projeto vive em **[`TCMine-Docs/`](TCMine-Docs/)** — um vault
**Obsidian** com decisões de arquitetura, conceitos e o detalhe vivo de cada componente.
Comece pelo índice curado:

- **[TCMine-Docs/wiki/index.md](TCMine-Docs/wiki/index.md)** — catálogo de todas as páginas
- [`wiki/entities/`](TCMine-Docs/wiki/entities/) — um doc por projeto/componente
- [`wiki/concepts/`](TCMine-Docs/wiki/concepts/) — padrões e convenções transversais
- [`wiki/decisions/`](TCMine-Docs/wiki/decisions/) — registros de decisão (ADR)
- [`wiki/log.md`](TCMine-Docs/wiki/log.md) — histórico cronológico das mudanças

Convenções de contribuição (idioma, tamanho de arquivo, code-behind Blazor, etc.) estão em
**[CLAUDE.md](CLAUDE.md)**.

---

## Rodar em produção (Docker)

Em produção você **não compila nada localmente** — usa a **imagem pública** já construída pelo
**GitHub Actions** (a cada tag `server-v*`) e publicada no Docker Hub:
**[`jocian/tcmine-server`](https://hub.docker.com/r/jocian/tcmine-server)**. Basta o `compose.yaml`
e um `.env` no host — nada de clonar o código-fonte (isso é o [modo de desenvolvimento](#desenvolvimento-local)).

A imagem é baseada no **SDK** .NET (não no runtime) porque o servidor **compila o launcher em runtime**
(painel `/admin/releases`) e carrega o `vpk` (Velopack CLI) + um **JRE** — a mesma imagem é reutilizada
para rodar cada instância de servidor Minecraft (Docker-out-of-Docker).

### Pré-requisitos

- **Docker** e **Docker Compose** no host (Linux recomendado para produção).
- Acesso ao **socket do Docker do host** (`/var/run/docker.sock`) — o servidor cria
  containers-irmãos para as instâncias Minecraft (**Docker-out-of-Docker**).
- Um **PostgreSQL** acessível (recomendado em produção). *SQLite é o padrão zero-config, mas para
  produção com múltiplas instâncias prefira Postgres.*
- Uma **chave da API do CurseForge** (configurada depois, pelo painel — não vai em variável).

### 1. Obter o `compose.yaml` (sem clonar)

Em produção basta o `compose.yaml` (ele já aponta para a imagem `jocian/tcmine-server:latest`). Crie
uma pasta e baixe só esse arquivo:

```bash
mkdir tcmine && cd tcmine
curl -O https://raw.githubusercontent.com/tiny-core/TCMine/master/compose.yaml
```

### 2. Configurar o banco via `.env`

O [`compose.yaml`](compose.yaml) lê as variáveis de banco de um arquivo **`.env`** na raiz
(**não versionado** — já está no `.gitignore`). Crie o seu:

```dotenv
# .env
DB_PROVIDER=Postgres
DB_HOST=db.seu-host.com
DB_PORT=5432
DB_NAME=tcmine
DB_USER=tcmine
DB_PASSWORD=troque-esta-senha
```

| Variável | O que é | Obrigatória |
|----------|---------|:-----------:|
| `DB_PROVIDER` | Engine do banco: `Postgres` ou `Sqlite` | não (`Sqlite`) |
| `DB_HOST` | Host do Postgres | sim (Postgres) |
| `DB_PORT` | Porta do Postgres | não (`5432`) |
| `DB_NAME` | Nome da database | sim (Postgres) |
| `DB_USER` | Usuário | sim (Postgres) |
| `DB_PASSWORD` | Senha | sim (Postgres) |

> **Alternativa:** em vez das quatro variáveis, você pode passar uma única
> `DB_CONNECTION=Host=...;Port=...;Database=...;Username=...;Password=...` — ela tem
> prioridade. A resolução completa está em
> [`DatabaseServiceCollectionExtensions.cs`](TCMine-Server.Infrastructure/Persistence/DatabaseServiceCollectionExtensions.cs)
> e em [persistence-dual-provider](TCMine-Docs/wiki/decisions/persistence-dual-provider.md).
>
> As migrations são aplicadas **automaticamente no boot** — você não precisa rodar
> `dotnet ef database update` no container.

### 3. Subir o servidor

```bash
# Puxa a imagem pública do Docker Hub e sobe. -d roda em background.
docker compose up -d
```

O `docker compose up` baixa `jocian/tcmine-server:latest` do Docker Hub (sem build local) e sobe na
porta **8080**. Para fixar uma versão em vez de `latest`, edite a linha `image:` do `compose.yaml`
(ex.: `jocian/tcmine-server:1.0.0`). Acompanhe os logs:

```bash
docker compose logs -f tcmine-server
```

### 4. Persistência (volume de dados)

**Todo** o estado do servidor vive num único diretório, montado por **bind-mount** no `compose.yaml`:

```yaml
volumes:
  - ./tcmine-data:/app/tcmine-data      # host → container
```

O que fica lá dentro (criado no boot): `updates/` (feed do launcher), `secrets/` (chaves do Data
Protection — **cifram o token do CurseForge**), `servers/` (instâncias Minecraft), `modpacks/`,
`mods/` (cache de jars) e `player-configs/` (sync das configs do jogador). **Sem esse bind, você
perde tudo isso ao recriar o container.**

**Trocar o local no host** — edite o lado esquerdo do bind (aceita caminho relativo ou absoluto):

```yaml
volumes:
  - /srv/tcmine/data:/app/tcmine-data
```

**⚠️ Regra obrigatória do Docker-out-of-Docker:** o servidor cria containers-irmãos (as instâncias
Minecraft) que montam subpastas de `tcmine-data` **direto do host**. Para os caminhos baterem, a
variável `ServerInstances__DataHostRoot` (no `environment`) tem de ser o **caminho no host que
corresponde a `/app`** — isto é, o diretório **pai** de `tcmine-data` no host. Se os dois não casarem,
as instâncias sobem apontando para um caminho vazio.

| Bind `volumes` (host → container) | `ServerInstances__DataHostRoot` correspondente |
|-----------------------------------|-----------------------------------------------|
| `./tcmine-data:/app/tcmine-data` | `${PWD}` |
| `/srv/tcmine/data:/app/tcmine-data` | `/srv/tcmine` |

> **Use bind-mount, não named volume:** as instâncias Minecraft precisam de um caminho de **host real**
> para re-montar os dados; um `docker volume` não expõe esse caminho e o DooD não funcionaria.

**Backup:** com o servidor parado, copie a pasta `tcmine-data/` **e** faça um dump do Postgres.

### 5. Primeiro acesso — criar o `Owner`

Abra **`http://SEU_HOST:8080`**. Na primeira execução, sem nenhum usuário, o servidor
redireciona para **`/setup`**, onde você cria a conta **Owner** (a senha é guardada com PBKDF2).
Os papéis disponíveis são `Owner` / `Admin` / `Operator` / `Viewer`.

### 6. Configurar os segredos de runtime (no painel)

Diferente do banco, os segredos de runtime **não** vão em variável de ambiente — são
configurados **pelo painel admin** e guardados no banco (o token do CurseForge fica **cifrado**
via Data Protection). Após logar como Owner, configure:

- **Token da API do CurseForge** — habilita busca/import de mods e o proxy `/v1/*`.
- **Azure Client Id / Tenant Id** — para o login Microsoft do launcher.
- **`PublicBaseUrl`** — a URL pública canônica do servidor (ex.: `https://tcmine.seu-dominio.com`).
  Usada para montar os links absolutos de download dos mods e o feed de update. **Defina isto
  se estiver atrás de um reverse proxy.**

### 7. (Recomendado) Reverse proxy + HTTPS

Em produção, coloque um reverse proxy (Nginx, Caddy, Traefik) na frente, terminando TLS e
encaminhando para a porta `8080` do container. Depois defina o `PublicBaseUrl` (passo 6) com a
URL pública `https://…`.

### 8. Publicar o launcher

Com o servidor no ar, vá em **Admin → Releases** para **compilar e publicar o launcher**
(`dotnet publish` + `vpk`, feito dentro do container). O servidor hospeda o feed Velopack em
`/updates`; a partir daí os launchers instalados se auto-atualizam. Detalhes em
[launcher-build-velopack](TCMine-Docs/wiki/concepts/launcher-build-velopack.md).

### Atualizar / manutenção

```bash
docker compose pull               # puxa a nova imagem publicada (jocian/tcmine-server:latest)
docker compose up -d              # recria o container com a nova imagem
docker compose down               # para tudo
```

Quando o painel avisar que há uma versão nova do servidor (faixa `server-v*`), atualizar é só o
`docker compose pull && docker compose up -d` acima.

Os dados persistem no bind-mount **`./tcmine-data`** (ver [Persistência](#4-persistência-volume-de-dados)):
o `docker compose up --build` reaproveita a pasta, então atualizar **não** apaga modpacks, servidores
nem configs. **Inclua `tcmine-data/` e o banco Postgres nos seus backups.**

---

## Desenvolvimento local

Sem Docker, para desenvolver:

```bash
# Build da solução inteira
dotnet build            # (usa TCMine.slnx)

# Servidor (dev → http://localhost:5244 / https://localhost:7002)
cd TCMine-Server && dotnet run

# Launcher (desktop, Avalonia)
cd TCMine-Launcher && dotnet run
```

Em dev, o banco/segredos de bootstrap saem de `appsettings.local.json` (fora do git); sem
config, cai no **SQLite** (`data-server/tcmine.db`). Migrations por provider:

```bash
dotnet ef migrations add <Nome> --project TCMine-Server.Infrastructure --context SqliteAppDbContext
dotnet ef migrations add <Nome> --project TCMine-Server.Infrastructure --context PostgresAppDbContext
```

## Estrutura do repositório

```
TCMine/
├── TCMine-Domain/                 # núcleo: entidades e regras puras
├── TCMine-Application/            # portas + contratos (DTOs record)
├── TCMine-Server.Infrastructure/  # EF Core, CurseForge, filesystem, Minecraft
├── TCMine-Launcher.Infrastructure/# infra do launcher (CmlLib, HTTP, FS)
├── TCMine-Design/                 # design system (ColorTokens)
├── TCMine-Server/                 # ASP.NET Core: API + painel admin (Blazor)
│   └── Dockerfile                 # imagem de produção
├── TCMine-Launcher/               # app desktop Avalonia
├── TCMine-IconGenerator/          # gerador de ícones/assets
├── TCMine-Docs/                   # base de conhecimento (vault Obsidian)
├── compose.yaml                   # orquestração de produção (Docker + DooD)
├── Directory.Packages.props       # versões NuGet centralizadas
├── CLAUDE.md                      # convenções do projeto
└── TCMine.slnx                    # solução
```

---

<sub>Documentação viva em [`TCMine-Docs/wiki/`](TCMine-Docs/wiki/index.md) · Convenções em
[`CLAUDE.md`](CLAUDE.md)</sub>
