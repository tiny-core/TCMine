# Lançar uma versão

O repositório abriga dois produtos, então a tag diz de qual se trata:

| Tag | O que dispara |
|---|---|
| `server-v0.2.0` | Constrói a imagem e publica no Docker Hub |
| `launcher-v0.1.0` | Launcher (`release-launcher.yml`, em windows-latest) |

O prefixo não é cosmético: sem ele, publicar o launcher reconstruiria o servidor
e vice-versa.

## Configurar uma vez

Em **Settings → Secrets and variables → Actions** do repositório:

| Nome | Tipo | Valor |
|---|---|---|
| `DOCKERHUB_USERNAME` | secret | Seu usuário do Docker Hub |
| `DOCKERHUB_TOKEN` | secret | Access token com escrita (Docker Hub → Account Settings → Personal access tokens). **Não** a senha da conta. |
| `DOCKERHUB_IMAGE` | variable | Nome do repositório da imagem, ex.: `jocian/tcmine-server` |

`DOCKERHUB_IMAGE` é variável e não segredo porque aparece no log de qualquer
forma — marcar como segredo só produziria `***` nas mensagens e dificultaria
entender o que foi publicado.

## Lançar o servidor

Antes da tag, escreva a entrada no [CHANGELOG.md](../CHANGELOG.md) e comite. A
release do GitHub é gerada a partir dos commits, que descrevem *o que mudou no
código*; o changelog descreve *o que mudou para quem usa* — são textos
diferentes, e o segundo não se escreve sozinho. Depois da tag ele fica
desalinhado do que foi publicado.

```bash
git tag server-v0.1.0
git push origin server-v0.1.0
```

O workflow roda as suítes de teste **e sobe a imagem** antes de publicar. Uma imagem publicada é
imutável na prática — alguém pode tê-la baixado no minuto seguinte —, então não
vale confiar num CI que passou numa versão anterior do código.

A fumaça (`scripts/smoke-image.sh`) sobe o container contra um PostgreSQL de
verdade e confere o que só quebra ali: as migrations aplicam, a página serve o
runtime do Blazor, e as colunas têm a largura que o domínio declara. Se ela
falhar, nada é publicado. Ela existe porque uma release já saiu com o
`blazor.web.js` respondendo 404 — a página pré-renderiza no servidor, então
continuava parecendo saudável, e só um diálogo que não abria denunciava.

Para rodar a mesma coisa na sua máquina:

```bash
./scripts/tc smoke-image
```

Tags geradas para `server-v0.1.0`:

- `0.1.0` — a versão exata
- `0.1` — acompanha os patches dessa linha
- `latest`

Um pré-lançamento (`server-v0.2.0-beta.1`) recebe **só** a versão exata. Nem
`latest` nem a tag curta: quem pede "a versão atual" não está pedindo um beta.

## A versão dentro da imagem

O número da tag entra na build e vira a versão do assembly, que é o que o
handshake devolve ao launcher em `serverVersion`. Sem isso toda imagem se
anunciaria como `1.0.0` — o padrão do SDK quando nada é informado.

Para conferir depois de publicar:

```bash
curl -s https://seu-dominio/api/handshake | jq .serverVersion
```

## Testar o workflow sem lançar

O workflow aceita disparo manual (**Actions → Publicar imagem do servidor → Run
workflow**). Nesse caso a imagem sai como `0.0.0-manual` e não recebe `latest`,
para um teste não virar a versão que os outros baixam.

## Lançar o launcher

```bash
git tag launcher-v0.1.0
git push origin launcher-v0.1.0
```

O workflow roda em **windows-latest** — o `EnableWindowsTargeting` deixa o Linux
compilar o host WPF, mas publicar um executável exige a plataforma. Ele corre os
testes, publica self-contained, empacota com o `vpk` e anexa o resultado a uma
release do GitHub.

O canal do Velopack deriva do **protocolo**, não da versão do produto — hoje
`win-x64-p2`. É o que permite publicar launcher 1.6, 1.7 e 1.8 sem release
nenhuma do servidor. O workflow lê o número do `Protocol.cs` em vez de o repetir,
porque dois lugares com o mesmo número acabam a discordar e publicar no canal
errado entrega uma atualização que o servidor nunca oferece.

### Pôr a release no ar

O workflow **não publica no servidor**, de propósito: a máquina que constrói não
devia ter credencial de escrita na que serve jogadores. Baixe os ficheiros da
release e copie-os para:

```
${TCMINE_ROOT}/updates/launcher/win-x64-p2/
```

É a pasta que `/updates/launcher/{canal}/` serve, derivada de `Storage:RootPath`.
Os launchers instalados encontram a novidade na abertura seguinte.

### O primeiro instalador

O feed serve **atualizações**, não a primeira instalação. O `*-Setup.exe` da
release é o que se entrega a quem ainda não tem o launcher — por download no
site, no Discord, onde fizer sentido.

### Quando o protocolo sobe

Subir `Protocol.Current` muda o canal, e um launcher no canal antigo **deixa de
ser aceite no handshake** (o mínimo sobe junto). Ele recebe "atualize", mas o
canal antigo já não recebe releases — então publique a versão nova ANTES de
subir o servidor, ou os jogadores ficam com uma instrução que não têm como
cumprir.
