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

### Atrás do Cloudflare

Funciona, com um aviso: o **Bot Fight Mode** injeta um script inline em cada
página HTML (`window.__CF$cv$params={r:'<cf-ray>'…}`), e a CSP do painel é
`script-src 'self'` — sem `unsafe-inline`, sem nonce. O navegador bloqueia esse
script e registra no console:

```
Executing inline script violates the following Content Security Policy directive 'script-src self'
```

**O painel não é afetado** — o que foi bloqueado é a detecção de bot do
Cloudflare, não código do TCMine. Mas o erro fica no console e confunde na hora
de diagnosticar outra coisa.

Como reconhecer que é isso, e não um problema seu: o hash sugerido pelo
navegador **muda a cada carregamento**. O script carrega o `CF-RAY` da resposta,
que é diferente em toda requisição — por isso não adianta liberar o hash, e é
também a assinatura do sintoma. Para confirmar:

```bash
curl -s https://seu-dominio/login | grep -o "__CF\$cv\$params[^,]*"
```

Saída não vazia = é o Cloudflare. Nenhuma página do TCMine serve script inline
(há teste que garante isso).

Para tirar o erro do console, desligue **Security → Bots → Bot Fight Mode** no
painel do Cloudflare. Não mexa na CSP para acomodá-lo: liberar `unsafe-inline`
abriria a porta de XSS que a política existe para fechar, por causa de um script
que nem é da aplicação.

O Cloudflare também acrescenta um segundo cabeçalho
`content-security-policy: frame-ancestors 'self'`. Cabeçalhos de CSP se somam
pela interseção, então isso só torna a política mais restrita — sem efeito
prático aqui, já que a nossa usa `frame-ancestors 'none'`.

## 5. Primeiro acesso

Abra `https://seu-dominio/setup` e crie a conta de administrador. A tela só
existe enquanto não houver nenhum usuário.

## ZimaOS e outros NAS

O painel do ZimaOS instala apps por formulário ou por YAML. **Use o YAML**: o
formulário não expressa `group_add` (a permissão para falar com o Docker) nem
bind mount com caminho idêntico, que são justamente as duas coisas que não podem
faltar.

### O caminho real, não o /DATA

O ZimaOS apresenta `/DATA` na interface, mas o caminho real no disco é
`/media/ZimaOS-HD/...`. Ao salvar, ele **reescreve** o volume: um
`origem:origem` vira `origem:/DATA/AppData/...`.

Com isso o painel funciona e **todo servidor de jogo sobe vazio** — o daemon
procura o caminho do container no host, não acha, cria uma pasta vazia e a
monta. Desde a 1.0.1 o arranque detecta e recusa, mas o conserto é o mesmo: usar
o caminho real dos dois lados, e **conferir o YAML depois de salvar**.

### Preparar por SSH

```bash
sudo mkdir -p /media/ZimaOS-HD/AppData/tcmine-server/{data,instances,postgres}
sudo chown -R 1654:1654 /media/ZimaOS-HD/AppData/tcmine-server/data \
                        /media/ZimaOS-HD/AppData/tcmine-server/instances
getent group docker | cut -d: -f3    # anote para o group_add
```

A pasta do Postgres fica **fora** do `chown`: a imagem dele ajusta o próprio
dono, e forçar 1654 ali a faz reclamar.

### YAML

```yaml
services:
  tcmine:
    image: SEU_USUARIO/tcmine-server:latest
    container_name: tcmine
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    ports:
      - "8099:8080"
    volumes:
      - type: bind
        source: /var/run/docker.sock
        target: /var/run/docker.sock
      - type: bind
        source: /media/ZimaOS-HD/AppData/tcmine-server
        target: /media/ZimaOS-HD/AppData/tcmine-server
    group_add:
      - "1000"
    environment:
      ASPNETCORE_URLS: http://+:8080
      Server__PublicUrl: https://tcmine.seudominio.com
      Server__Name: TCMine
      Server__AzureClientId: ""
      Storage__RootPath: /media/ZimaOS-HD/AppData/tcmine-server
      Database__Provider: Postgres
      Database__Host: postgres
      Database__Password: TROQUE

  postgres:
    image: postgres:17-alpine
    container_name: tcmine-postgres
    restart: unless-stopped
    volumes:
      - /media/ZimaOS-HD/AppData/tcmine-server/postgres:/var/lib/postgresql/data
    environment:
      POSTGRES_DB: tcmine
      POSTGRES_USER: tcmine
      POSTGRES_PASSWORD: TROQUE
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tcmine"]
      interval: 10s
      timeout: 5s
      retries: 5
```

A porta `8099` evita 80 e 443, que o próprio ZimaOS usa. Confira com
`ss -tlnp | grep 8099`.

O Postgres não publica porta: quem fala com ele é só o TCMine, pela rede interna
do compose.

### Portas — são duas coisas diferentes

**Servidores de jogo:** encaminhe **TCP** no roteador para o IP do ZimaOS. A
porta de cada servidor vem do `ConnectAddress` que você digita ao criá-lo:
`mc.exemplo.com:25566` publica na 25566; sem porta, cai na 25565. **Cada
servidor precisa de uma porta diferente** — dois com a mesma fazem o segundo
falhar ao subir.

**Painel:** não encaminhe. Ele tem o socket do Docker, ou seja, controle total do
NAS.

### TLS pelo Cloudflare Tunnel

Se você já usa Cloudflare, é o caminho mais limpo: não abre porta no roteador,
resolve o certificado e **envia `X-Forwarded-Proto`**, sem o qual o painel
responde 500 em tudo. Aponte o tunnel para `http://IP_DO_ZIMAOS:8099`.

O tunnel serve para o painel, **não para o Minecraft**: o jogo é TCP puro, e o
plano gratuito não faz proxy disso. A porta do jogo precisa de encaminhamento
direto no roteador.

### Duas limitações de rede doméstica

- **A porta 25 de saída** é bloqueada por praticamente todo provedor
  residencial, então o servidor de e-mail próprio **não vai entregar nada** a
  partir de casa. Para recuperação de senha funcionar, configure um SMTP externo
  na aba E-mail.
- **IP residencial muda.** Configure DDNS, ou os jogadores perdem o servidor na
  próxima renovação. O tunnel resolve isso só para o painel.

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
