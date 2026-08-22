# TCMine

Ecossistema para distribuir e jogar modpacks de Minecraft. Dois produtos, um
repositório.

**TCMine Server** — painel web que cria e publica modpacks, ingere mods do
Modrinth e do CurseForge, e orquestra os servidores de jogo como containers
Docker. Funcional e publicado como imagem, ainda em `0.x`: o conjunto de
funcionalidades está inteiro, mas a instalação em cenários variados continua
revelando arestas. O `1.0.0` fica reservado para quando isso parar de acontecer.

**TCMine Launcher** — cliente desktop que instala e atualiza as instâncias do
jogador. Em construção; o lado do servidor que ele consome já existe.

O fluxo central: o servidor publica **manifestos completos** de cada versão, e o
launcher reconcilia o disco do jogador contra o manifesto — baixa o que falta,
apaga o que sobrou. É um modelo declarativo: o manifesto descreve o estado
final, e o launcher faz o disco convergir para ele.

## O que o servidor faz

- **Modpacks** com versões imutáveis: uma versão publicada nunca muda, então nem
  mod despublicado nem cota de API esgotada quebram quem já está jogando.
- **Importação e atualização** de packs do Modrinth e do CurseForge, com merge de
  três vias — o que o autor mudou entra sozinho, o que você customizou é
  preservado, e só os conflitos reais são perguntados.
- **Servidores de jogo** como containers `itzg/minecraft-server`, com console ao
  vivo, comandos por RCON e métricas.
- **Backups de mundo**, inclusive a quente: com o servidor no ar, o autosave é
  pausado, o mundo vai para o disco, a cópia é feita e o autosave religa.
- **Convites e papéis por servidor**, com login de jogador pelo perfil Minecraft
  verificado.
- **Storage endereçado por conteúdo** (SHA-256), com deduplicação automática.

## Rodar

Requer Linux com Docker. O guia completo está em
[docs/DEPLOY.md](docs/DEPLOY.md), incluindo uma seção para
[ZimaOS e NAS](docs/DEPLOY.md#zimaos-e-outros-nas).

```bash
sudo mkdir -p /opt/tcmine && sudo chown -R 1654:1654 /opt/tcmine
cp .env.example .env      # ajuste TCMINE_ROOT, DOCKER_GID e TCMINE_PUBLIC_URL
docker compose up -d
```

Depois abra `https://seu-dominio/setup` para criar a conta de administrador.

Dois requisitos que não dá para pular, e cujo sintoma não aponta a causa:

- **Proxy reverso terminando TLS**, enviando `X-Forwarded-Proto`. Sem esse
  cabeçalho o cookie de sessão não pode ser emitido e toda página responde 500.
- **A pasta de dados precisa pertencer ao usuário `1654`**, que é como o
  container roda.

O painel recebe o socket do Docker, o que lhe dá controle total da máquina. Não
exponha essa porta diretamente na internet.

## Desenvolver

```bash
dotnet run --project src/server/TCMine.Server.Web
```

Verificação:

```bash
./scripts/tc check      # build + testes, com saída curta
./scripts/tc test '*Invite*'
```

Use `scripts/tc` em vez de `dotnet test`: o SDK do .NET 10 removeu o caminho
VSTest, e o runner do xUnit v3 é o próprio executável de cada suíte.

## Arquitetura

Clean Architecture, com a dependência sempre apontando para dentro — e isso é
**verificado por testes** (NetArchTest): inverter uma dependência deixa o build
vermelho.

```
Domain ← Application (contratos, portas, casos de uso) ← Infrastructure ← Web
```

Duas regras que atravessam o projeto:

- **A autorização mora no caso de uso, não na borda.** A borda é plural — hub
  SignalR, endpoint HTTP, componente Blazor — e cada borda nova esquece de novo.
- **Falhas de regra de negócio são `Result`, não exceção.** Exceção fica para o
  inesperado.

Stack: .NET 10, Blazor Server, MudBlazor, EF Core (SQLite ou PostgreSQL),
SignalR, Docker Engine API.

Detalhes de arquitetura e as decisões já tomadas estão em
[CLAUDE.md](CLAUDE.md).

## Lançar uma versão

Tags com prefixo separam os dois produtos:

```bash
git tag server-v0.2.0 && git push origin server-v0.2.0
```

Isso roda os testes, publica a imagem no Docker Hub e cria a release. Ver
[docs/RELEASE.md](docs/RELEASE.md).

O que mudou em cada versão está no [CHANGELOG.md](CHANGELOG.md).

## Licença

GPL-3.0.
