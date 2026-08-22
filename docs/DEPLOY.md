# Implantação do TCMine Server

Guia para subir o painel em um host Linux com Docker.

> **Linux, e não Windows.** O TCMine pede ao daemon do Docker que monte a pasta
> de cada instância dentro do container do jogo, e quem interpreta esse caminho
> é o daemon. Em Docker Desktop no Windows o daemon vive numa VM Linux, então o
> caminho que o painel enxerga nunca coincide com o que o daemon enxerga — e o
> servidor de jogo sobe com uma pasta vazia, sem erro nenhum. Para desenvolver
> no Windows, use `dotnet run`; o compose é para o host de produção.

## 1. Preparar a pasta de dados

```bash
sudo mkdir -p /opt/tcmine
sudo chown -R 1654:1654 /opt/tcmine
```

O `1654` é o usuário não-root da imagem oficial do .NET, que é como o container
roda. Sem esse `chown`, o arranque falha dizendo exatamente qual pasta não pôde
criar — a mensagem é clara, mas o problema é este.

## 2. Configurar

```bash
cp .env.example .env
```

Ajuste no `.env`:

| Variável | O que é |
|---|---|
| `TCMINE_ROOT` | Pasta do passo 1. Montada no **mesmo caminho** dentro do container — ver a nota abaixo. Vira `Storage__RootPath`, de onde saem banco, blobs, instâncias e chaves. |
| `DOCKER_GID` | GID do grupo dono do socket: `getent group docker \| cut -d: -f3` |
| `TCMINE_PUBLIC_URL` | Endereço público, com https. Vai no `tcmine.json` e no feed do launcher. |
| `TCMINE_AZURE_CLIENT_ID` | Client ID da app Azure do login com a Microsoft. |

### Uma raiz, quatro caminhos

`Storage__RootPath` preenche sozinho o que não for declarado:

| Derivado | Caminho |
|---|---|
| Banco (SQLite) | `{raiz}/data/tcmine.db` |
| Blobs | `{raiz}/data/blobs` |
| Instâncias | `{raiz}/instances` |
| Chaves | `{raiz}/data/keys` |

Para separar um deles — blobs num disco maior, por exemplo — declare a chave
específica (`BlobStorage__RootPath`) e ela ganha da derivação.

**Por que o mesmo caminho dos dois lados:** o painel manda o daemon montar
`{TCMINE_ROOT}/instances/{id}` no container do jogo. O daemon resolve esse
caminho **no host**. Se dentro do container a raiz fosse `/app/data` e no host
`/opt/tcmine`, o Docker criaria `/app/data/...` vazio no host e o montaria: o
servidor subiria sem mods e sem mundo, silenciosamente. O arranque recusa subir com caminho relativo, e
também quando detecta que o bind mount trocou o nome da pasta no caminho — que é
o que interfaces de NAS costumam fazer sozinhas.

> **Painéis de NAS reescrevem volumes.** O ZimaOS, por exemplo, transforma
> `/media/ZimaOS-HD/AppData/tcmine:/media/ZimaOS-HD/AppData/tcmine` em
> `/media/ZimaOS-HD/AppData/tcmine:/DATA/AppData/tcmine` — com o painel
> funcionando e todo servidor de jogo subindo vazio. Use o caminho real do disco
> nos dois lados e confira o YAML depois de salvar. Se a checagem de arranque
> errar no seu arranjo, desligue com `Storage__SkipMountCheck=true`.

## 3. Subir

```bash
docker compose up -d
```

Para PostgreSQL em vez de SQLite:

```bash
docker compose --profile postgres up -d
```

e no `.env`:

```
TCMINE_DB_PROVIDER=Postgres
```

O banco aceita campos separados, que é o que se recomenda:

| Variável | Padrão |
|---|---|
| `Database__Host` | — (obrigatório para Postgres) |
| `Database__Port` | `5432` |
| `Database__Name` | `tcmine` |
| `Database__Username` | `tcmine` |
| `Database__Password` | — |

Prefira os campos à connection string inteira: a senha é escapada pelo driver,
e uma senha com `;` ou `=` quebra uma string montada à mão — com o erro
chegando como "autenticação falhou", que manda conferir uma senha que está
certa.

Ainda assim, `Database__ConnectionString` continua valendo e **ganha dos
campos**, para quem precisa de parâmetros que eles não cobrem (SSL, timeout,
pool).

## 4. Proxy reverso — obrigatório

O painel **não termina TLS**: isso é do proxy. E ele precisa receber
`X-Forwarded-Proto`, senão o cookie de sessão (marcado `Secure` fora de
Development) não pode ser emitido e **toda página responde 500**. É o erro mais
fácil de cometer aqui, e o sintoma não menciona proxy nenhum.

Exemplo com Caddy:

```
tcmine.exemplo.com {
    reverse_proxy localhost:8080
}
```

O Caddy manda `X-Forwarded-Proto` por padrão. Com nginx, declare:

```nginx
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header Host $host;
```

Para conferir sem proxy, simule o cabeçalho:

```bash
curl -H 'X-Forwarded-Proto: https' -o /dev/null -w '%{http_code}\n' http://localhost:8080/setup
```

## 5. Primeiro acesso

Abra `https://seu-dominio/setup` e crie a conta de administrador. A tela só
existe enquanto não houver nenhum usuário.

## O que o compose concede ao painel

O container recebe `/var/run/docker.sock`. Isso lhe dá o poder de criar
containers — e, por consequência, **controle total desta máquina**. É o que
permite orquestrar os servidores de jogo e o servidor de e-mail, e é a razão de
o painel exigir autenticação e de o proxy ser obrigatório. Não exponha esta
porta diretamente na internet.

## Atualizar

```bash
docker compose build
docker compose up -d
```

As migrations do banco são aplicadas no arranque. Se você tem um pipeline e
prefere controlar o momento, desligue com `Database__AutoMigrate=false` e
aplique-as antes de subir a versão nova.

## Backup

O que importa está sob `TCMINE_ROOT`:

- `data/tcmine.db` — o catálogo (ou o volume do Postgres, se for o caso)
- `data/blobs` — os arquivos dos modpacks, endereçados por hash
- `data/keys` — chaves de proteção de dados. **Perder isto derruba as sessões e
  torna ilegíveis os segredos gravados** (chave do CurseForge, senha do SMTP).
- `instances/` — os mundos dos servidores
