# Implantação do TCMine Server

Guia para subir o painel num host Linux com Docker — Ubuntu no exemplo,
mas serve para qualquer distribuição com Docker.

> **Linux, e não Windows.** O TCMine pede ao daemon do Docker que monte a pasta
> de cada instância dentro do container do jogo, e quem interpreta esse caminho
> é o daemon. Em Docker Desktop no Windows o daemon vive numa VM Linux, então o
> caminho que o painel enxerga nunca coincide com o que o daemon enxerga — e o
> servidor de jogo sobe com uma pasta vazia, sem erro nenhum. Para desenvolver
> no Windows, use `dotnet run`; o compose é para o host de produção.

## 1. Docker no host

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo systemctl enable --now docker
```

O `enable` não é redundante: sem ele o Docker não volta após um reboot, e
o `restart: unless-stopped` do compose não tem quem o execute — a máquina
reinicia por uma atualização de kernel e o painel simplesmente não volta.

## 2. Preparar a pasta de dados

```bash
sudo mkdir -p /opt/tcmine
sudo chown -R 1654:1654 /opt/tcmine
getent group docker | cut -d: -f3    # anote para o DOCKER_GID
```

O `1654` é o usuário não-root da imagem oficial do .NET, que é como o container
roda. Sem esse `chown`, o arranque falha dizendo exatamente qual pasta não pôde
criar — a mensagem é clara, mas o problema é este.

Se for usar PostgreSQL com a pasta no disco, deixe-a **fora** deste `chown`: a
imagem do Postgres ajusta o próprio dono, e forçar `1654` ali a faz reclamar no
arranque.

## 3. Configurar

```bash
cp .env.example .env
```

Ajuste no `.env`:

| Variável                          | O que é                                                                                                                                                                                                                                                           |
|-----------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `TCMINE_ROOT`                     | Pasta do passo 1. Montada no **mesmo caminho** dentro do container — ver a nota abaixo. Vira `Storage__RootPath`, de onde saem banco, blobs, instâncias e chaves.                                                                                                 |
| `DOCKER_GID`                      | GID do grupo dono do socket: `getent group docker \| cut -d: -f3`                                                                                                                                                                                                 |
| `TCMINE_PUBLIC_URL`               | Endereço público, com https. Vai no `tcmine.json` e no feed do launcher — e **dentro do instalador do launcher**, que a imagem empacota ao subir: quem instala pela página pública não digita endereço. Sem ele o instalador sai sem endereço e o jogador digita. |
| `LauncherUpdates__PublishBundled` | `false` desliga o empacotamento do launcher no arranque (feed mantido à mão — ver [RELEASE.md](RELEASE.md#o-launcher)).                                                                                                                                           |
| `TCMINE_AZURE_CLIENT_ID`          | Client ID da app Azure do login com a Microsoft.                                                                                                                                                                                                                  |
| `Updates__Enabled`                | `false` desliga o aviso de versão nova no painel (ele consulta as releases do GitHub a cada seis horas). Útil em máquina sem saída para a internet.                                                                                                               |

### Uma raiz, quatro caminhos

`Storage__RootPath` preenche sozinho o que não for declarado:

| Derivado       | Caminho                 |
|----------------|-------------------------|
| Banco (SQLite) | `{raiz}/data/tcmine.db` |
| Blobs          | `{raiz}/data/blobs`     |
| Instâncias     | `{raiz}/instances`      |
| Chaves         | `{raiz}/data/keys`      |

Para separar um deles — blobs num disco maior, por exemplo — declare a chave
específica (`BlobStorage__RootPath`) e ela ganha da derivação.

**Por que o mesmo caminho dos dois lados:** o painel manda o daemon montar
`{TCMINE_ROOT}/instances/{id}` no container do jogo. O daemon resolve esse
caminho **no host**. Se dentro do container a raiz fosse `/app/data` e no host
`/opt/tcmine`, o Docker criaria `/app/data/...` vazio no host e o montaria: o
servidor subiria sem mods e sem mundo, silenciosamente. O arranque recusa subir com caminho relativo, e
também quando detecta que o bind mount trocou o nome da pasta no caminho — que é
o que interfaces de NAS costumam fazer sozinhas.

> **Se instalar por um painel de NAS, confira o YAML após salvar.** Alguns
> reescrevem o volume para um caminho de apresentação (`/DATA/...`), e aí o
> painel funciona enquanto todo servidor de jogo sobe vazio. O arranque detecta e
> recusa; se a checagem errar no seu arranjo, desligue com
> `Storage__SkipMountCheck=true`. Num Ubuntu comum isto não acontece.

## 4. Subir

```bash
docker compose up -d
```

Para PostgreSQL em vez de SQLite:

```bash
docker compose --profile postgres up -d
```

E no `.env`:

```
TCMINE_DB_PROVIDER=Postgres
```

O banco aceita campos separados, que é o que se recomenda:

| Variável             | Padrão                        |
|----------------------|-------------------------------|
| `Database__Host`     | — (obrigatório para Postgres) |
| `Database__Port`     | `5432`                        |
| `Database__Name`     | `tcmine`                      |
| `Database__Username` | `tcmine`                      |
| `Database__Password` | —                             |

Prefira os campos à connection string inteira: a senha é escapada pelo driver,
e uma senha com `;` ou `=` quebra uma string montada à mão — com o erro
chegando como "autenticação falhou", que manda conferir uma senha que está
certa.

Ainda assim, `Database__ConnectionString` continua valendo e **ganha dos
campos**, para quem precisa de parâmetros que eles não cobrem (SSL, timeout,
pool).

### Usar a imagem publicada em vez de construir

O compose do repositório constrói a imagem. Em produção o normal é consumir a
que o `release.yml` publicou: um `docker-compose.yml` com `image:` no
lugar de `build:`.

```yaml
services:
  tcmine:
    image: jocian/tcmine-server:latest
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - /opt/tcmine:/opt/tcmine
    group_add:
      - "999"                     # o GID que você anotou
    environment:
      ASPNETCORE_URLS: http://+:8080
      Server__PublicUrl: https://tcmine.exemplo.com
      # Opcional, nuvem de itens (mod tccloud): endereço do TCMine visto de DENTRO
      # dos containers de jogo, quando o PublicUrl não é alcançável de lá.
      # Server__CloudUrl: http://tcmine:8080
      Server__Name: TCMine
      Storage__RootPath: /opt/tcmine
      Database__Provider: Sqlite
```

Repare no `127.0.0.1:` antes da porta. Sem ele o Docker publica em **todas** as
interfaces e escreve a regra direto no `iptables`, **passando por cima do
`ufw`** — você fecharia a porta no firewall e ela continuaria aberta para a
internet. Como o painel tem o socket do Docker, isso é controle total da máquina
exposto sem autenticação até a tela de primeiro acesso ser usada. Prenda ao
loopback e deixe o proxy reverso ser a única porta de entrada.

## 5. Firewall

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80,443/tcp          # o proxy reverso
sudo ufw allow 25565/tcp           # cada servidor de jogo, uma porta
sudo ufw enable
```

**O painel não entra nesta lista.** Ele fica no loopback e só o proxy fala com
ele.

## 6. Proxy reverso — obrigatório

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
curl -H 'X-Forwarded-Proto: https' -o /dev/null -w '%{http_code}\n' http://localhost:8080/admin/setup
```

### Atrás do Cloudflare

Funciona, com um aviso: o **Bot Fight Mode** injeta um roteiro inline em cada
página HTML (`window.__CF$cv$params={r:'<cf-ray>'…}`), e a CSP do painel é
`script-src 'self'` — sem `unsafe-inline`, sem nonce. O navegador bloqueia esse
roteiro e registra no console:

```
Executing inline script violates the following Content Security Policy directive 'script-src self'
```

**O painel não é afetado** — o que foi bloqueado é a detecção de bot do
Cloudflare, não código do TCMine. Mas o erro fica no console e confunde na hora
de diagnosticar outra coisa.

Como reconhecer que é isso, e não um problema seu: o hash sugerido pelo
navegador **muda a cada carregamento**. O roteiro carrega o `CF-RAY` da resposta,
diferente em toda requisição — por isso não adianta liberar o hash, e é
também a assinatura do sintoma. Para confirmar:

```bash
curl -s https://seu-dominio/login | grep -o "__CF\$cv\$params[^,]*"
```

Saída não vazia = é o Cloudflare. Nenhuma página do TCMine serve roteiro inline (há teste que garante isso).

Para tirar o erro do console, desligue **Security → Bots → Bot Fight Mode** no
painel do Cloudflare. Não mexa na CSP para acomodá-lo: liberar `unsafe-inline`
abriria a porta de XSS que a política existe para fechar, devido a um roteiro
que nem é da aplicação.

O Cloudflare também acrescenta um segundo cabeçalho
`content-security-policy: frame-ancestors 'self'`. Cabeçalhos de CSP se somam
pela interseção, então isso só torna a política mais restrita — sem efeito
prático aqui, já que a nossa usa `frame-ancestors 'none'`.

## 7. Primeiro acesso

Não há conta de e-mail e senha: o painel entra pela **conta Microsoft**, e o
launcher pelo perfil Minecraft da mesma pessoa — as duas viram uma conta só.

1. Registre um aplicativo no Entra ID (*portal.azure.com → Microsoft Entra ID → Registros
   de aplicativo → Novo registro*), **somente contas Microsoft pessoais**.
2. Na plataforma **Web**, registre `https://seu-dominio/auth/microsoft/callback`
   (é o login do painel). Na plataforma **cliente público / nativo**, os dois URIs
   do launcher: `http://localhost` e
   `ms-appx-web://microsoft.aad.brokerplugin/{client-id}`. A tela de
   Configurações mostra os três prontos para copiar.
3. Abra `https://seu-dominio/admin/setup`, cole o client ID e entre com a
   Microsoft. **A primeira conta vira a administradora da instalação.** A tela só
   existe enquanto não houver nenhum usuário; depois, o client ID se troca em
   Configurações.

O `TCMINE_AZURE_CLIENT_ID` do `.env` contínua valendo como semente: com ele
preenchido, o `/admin/login` já oferece "Entrar com a Microsoft" e o passo 3 se
reduz a entrar.

## Portas dos servidores de jogo

Cada servidor publica uma porta própria nesta máquina, escolhida no formulário
(um servidor novo recebe a primeira livre da faixa em *Configurações → Rede*,
padrão 25565–25599). Abra a faixa no firewall e, se for rede doméstica,
encaminhe-a no roteador — a faixa inteira de uma vez evita voltar ao roteador a
cada servidor novo.

O **endereço** que os jogadores recebem é montado pelo TCMine:

- campo de endereço do servidor **vazio** → o endereço público da instalação
  mais a porta do servidor. O endereço público é o que estiver em
  *Configurações → Rede* (um domínio ou DDNS) ou, na falta dele, o IP público
  detectado;
- endereço **sem porta** (`mc.exemplo.com`) → recebe a porta do servidor;
- endereço **com porta** (`mc.exemplo.com:25565`) → sai como está escrito. É
  para quando o roteador expõe uma porta e entrega em outra.

### Subdomínio por servidor (opcional)

Com o domínio na Cloudflare, cada servidor pode ter um nome próprio e sem porta
(`sobrevivencia.exemplo.com`). Em *Configurações → Rede → Subdomínios*:

1. Crie na Cloudflare um token **só para isto** (*My Profile → API Tokens →
   Create Token → Edit zone DNS*), restrito à zona do domínio.
2. Cole o token, o **Zone ID** (página *Overview* do domínio) e o domínio, e
   salve.
3. Preencha o campo *Subdomínio* no formulário de cada servidor e use
   *Sincronizar agora*.

O TCMine cria um registro SRV por servidor e, se o endereço público não for um
nome, um registro A (`mc.exemplo.com` por padrão) com o IP público. Ele só
altera e apaga registros que ele mesmo criou — reconhece-os por um comentário
que começa por `tcmine:` — e não cria nada por cima de um registro seu com o
mesmo nome. Tirar o token não apaga os registros já criados.

O registro A é criado em modo **DNS only** (nuvem cinza): o proxy da Cloudflare
não passa o protocolo do jogo. SRV só é consultado pelo Minecraft **Java**.

O IP público é detectado consultando a Cloudflare e, se ela falhar, o ipify — o
container precisa de saída HTTPS para `www.cloudflare.com` ou `api.ipify.org`.
Com o endereço público preenchido, nenhuma dessas consultas é necessária para
os jogadores entrarem.

## Se for hospedar em casa

Uma limitação que não é do TCMine e morde sempre:

- **IP residencial muda.** Configure DDNS e ponha o nome em *Configurações →
  Rede → Endereço público*, ou os jogadores perdem o endereço na próxima
  renovação.
- **CGNAT.** Muitas operadoras dividem um IP público entre vários clientes. O
  painel detecta esse IP normalmente, mas o encaminhamento de portas do roteador
  não funciona e ninguém de fora entra. Sinal típico: o IP da WAN mostrado pelo
  roteador é diferente do detectado. A saída é pedir um IP público à operadora.

Um Cloudflare Tunnel resolve o TLS do painel sem abrir porta no roteador, e
envia o `X-Forwarded-Proto` de que o painel precisa. Ele **não serve para o
Minecraft**: o jogo é TCP puro e o plano gratuito não faz proxy disso — a porta
do jogo continua a precisar de encaminhamento direto.

## Nenhum servidor de jogo inicia

Quase sempre é o socket do Docker. O painel roda como um usuário sem privilégio (`1654`), e o socket pertence ao grupo
`docker` com modo `660` — quem não está
nesse grupo não o abre, e toda operação de container falha com *permission
denied*.

Confira o GID do socket no host e os grupos do container:

```bash
stat -c 'gid=%g grupo=%G modo=%a' /var/run/docker.sock
docker exec <container-do-tcmine> id
```

Se o GID do socket não aparecer nos grupos, acrescente-o ao `group_add` do
container e recrie-o. O número vária por distribuição — 999 no Ubuntu, outro
valor noutras. Não chute: leia o `stat`.

A partir da 0.1.8 o próprio arranque avisa: procure no log a linha
`Sem acesso ao Docker`.

## O que o compose concede ao painel

O container recebe `/var/run/docker.sock`. Isso lhe atribui o poder de criar
containers — e, por consequência, **controle total desta máquina**. É o que
permite orquestrar os servidores de jogo, e é a razão de
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
  torna ilegíveis os segredos gravados** (chave do CurseForge).
- `instances/` — os mundos dos servidores
