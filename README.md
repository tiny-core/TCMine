# TCMine

Ecossistema para distribuir e jogar modpacks de Minecraft. Dois produtos, um
repositório.

**TCMine Server** — painel web que cria e publica modpacks, ingere mods do
Modrinth e do CurseForge, e orquestra os servidores de jogo como containers
Docker. Estável desde a **1.0.0**, publicado como imagem no Docker Hub — ver o
[compromisso de estabilidade](#compromisso-de-estabilidade).

**TCMine Launcher** — cliente desktop (Windows) que instala, atualiza e **abre**
as instâncias do jogador. O laço está fechado: parear com o servidor, entrar com
a conta Microsoft, instalar um modpack, e jogar.

O fluxo central: o servidor publica **manifestos completos** de cada versão, e o
launcher reconcilia o disco do jogador contra o manifesto — baixa o que falta,
apaga o que sobrou. É um modelo declarativo: o manifesto descreve o estado
final, e o launcher faz o disco convergir para ele.

## O que o launcher faz

- **Entra com a conta Microsoft** — Microsoft → Xbox Live → XSTS → Minecraft,
  com o broker do Windows quando disponível e o navegador do sistema quando não.
  Nunca uma WebView embutida.
- **Instala e atualiza** reconciliando o disco contra o manifesto. Atualizar
  preserva o mundo, as configurações e a RAM escolhida — e tira uma cópia do
  mundo antes de mexer em qualquer coisa.
- **Abre o jogo**, gerindo o Java que cada versão pede e instalando o loader
  (NeoForge, Fabric, Quilt, Forge). Mostra o registro do jogo, então um crash
  deixa de ser "fechou sozinho".
- **Canal alpha** separado: uma instância alpha acompanha pré-lançamentos e
  nunca salta para o canal estável.
- **Funciona sem o servidor no ar.** O que está no disco continua jogável; só o
  catálogo e as novidades ficam de fora.
- **Atualiza-se sozinho**, pelo feed que o próprio TCMine Server publica.

## O que o servidor faz

- **Modpacks** com versões imutáveis: uma versão publicada nunca muda, então nem
  mod despublicado nem cota de API esgotada quebram quem já está jogando.
- **Importação e atualização** de packs do Modrinth e do CurseForge, com merge de
  três vias — o que o autor mudou entra sozinho, o que você customizou é
  preservado, e só os conflitos reais são perguntados. Você escolhe a versão do
  pack (e de cada mod) na hora de importar.
- **Ingestão banco → disco → rede**: um mod que qualquer modpack já trouxe é
  reaproveitado sem consultar a origem nem baixar de novo.
- **Servidores de jogo** como containers `itzg/minecraft-server`, rodando a
  versão do Minecraft e o loader do modpack, com console ao vivo, comandos por
  RCON e métricas.
- **Backups de mundo**, inclusive a quente: com o servidor no ar, o autosave é
  pausado, o mundo vai para o disco, a cópia é feita e o autosave religa.
- **Uma conta por pessoa, pela Microsoft**: o painel entra pela Microsoft, o
  launcher pelo perfil Minecraft verificado, e as duas são a mesma conta.
- **Convites, pedidos de acesso e papéis por servidor**; servidores sem
  whitelist aparecem para qualquer jogador autenticado.
- **Nuvem de itens** para o mod `tccloud` — ver
  [docs/CLOUD-STORAGE.md](docs/CLOUD-STORAGE.md).
- **Storage endereçado por conteúdo** (SHA-256), com deduplicação automática.

## Rodar

Requer Linux com Docker. O guia completo — Docker, firewall, proxy reverso — está
em [docs/DEPLOY.md](docs/DEPLOY.md).

```bash
sudo mkdir -p /opt/tcmine && sudo chown -R 1654:1654 /opt/tcmine
cp .env.example .env      # ajuste TCMINE_ROOT, DOCKER_GID e TCMINE_PUBLIC_URL
docker compose up -d
```

Depois abra `https://seu-dominio/admin/setup`, informe o client ID do app que você
registrou no Entra ID e entre com a sua conta Microsoft: a primeira conta vira a
administradora da instalação. O passo a passo do registro está em
[docs/DEPLOY.md](docs/DEPLOY.md).

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

## Compromisso de estabilidade

A partir da 1.0.0, o que segue só muda de forma incompatível numa versão MAIOR
(2.0.0), e sempre com instrução de atualização no [CHANGELOG.md](CHANGELOG.md):

- **O protocolo do launcher** (`Protocol.Current`, hoje 2): os métodos do hub, a
  aridade deles e os endpoints `/api/v1/*`. Um launcher publicado continua a
  falar com qualquer servidor 1.x.
- **A API da nuvem de itens** (`/api/cloud/v1`) que o mod `tccloud` usa.
- **A configuração**: as chaves de `appsettings`/variáveis de ambiente
  documentadas em [docs/DEPLOY.md](docs/DEPLOY.md) e o layout da pasta de dados
  (`TCMINE_ROOT`).
- **O banco**: atualizar dentro da 1.x é só trocar a imagem — as migrations
  aplicam-se no arranque, sempre para a frente.

Fora do compromisso: o HTML do painel, os nomes internos de classes e tabelas
(o banco é do TCMine, não uma API) e qualquer coisa marcada como experimental.

## Lançar uma versão

O servidor sai por tag:

```bash
git tag server-v0.4.0 && git push origin server-v0.4.0     # imagem no Docker Hub
```

O launcher vai **dentro da imagem**: ao subir, o servidor o publica na pasta de
atualizações com o próprio endereço embutido, e o jogador que instala pela
página pública não digita nada. O número do launcher vive em
`src/launcher/VERSION`, e a release recusa sair se o launcher mudou sem ele
subir — ver [docs/RELEASE.md](docs/RELEASE.md#o-launcher).

O que mudou em cada versão está no [CHANGELOG.md](CHANGELOG.md).

## Licença

GPL-3.0.
