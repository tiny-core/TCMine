# Lançar uma versão

A versão do servidor mora em `src/server/VERSION`. **Subir esse número no
`master` é o que publica**: o workflow `release.yml` roda os testes, constrói a
imagem, envia ao Docker Hub e, só no fim, cria a tag `server-v<versão>` e a
release no GitHub.

A tag é resultado da publicação, e não gatilho. Uma tag `server-v*` só existe
se a imagem daquela versão chegou ao Docker Hub — não crie tags `server-v*` à
mão.

O launcher não tem publicação própria: ele vai **dentro da imagem do servidor**
e é publicado no feed quando o container sobe — ver [O launcher](#o-launcher).

## Configurar uma vez

Em **Settings → Secrets and variables → Actions** do repositório:

| Nome                 | Tipo     | Valor                                                                                                        |
|----------------------|----------|--------------------------------------------------------------------------------------------------------------|
| `DOCKERHUB_USERNAME` | secret   | Seu usuário do Docker Hub                                                                                    |
| `DOCKERHUB_TOKEN`    | secret   | Access token com escrita (Docker Hub → Account Settings → Personal access tokens). **Não** a senha da conta. |
| `DOCKERHUB_IMAGE`    | variable | Nome do repositório da imagem, ex.: `jocian/tcmine-server`                                                   |

`DOCKERHUB_IMAGE` é variável e não segredo porque aparece no log de qualquer
forma — marcar como segredo só produziria `***` nas mensagens e dificultaria
entender o que foi publicado.

## Lançar o servidor

1. Escreva a entrada no [CHANGELOG.md](../CHANGELOG.md). A release do GitHub é
   gerada a partir dos commits, que descrevem *o que mudou no código*; o
   changelog descreve *o que mudou para quem usa* — são textos diferentes, e o
   segundo não se escreve sozinho.
2. Se o launcher mudou, suba `src/launcher/VERSION` (ver [O launcher](#o-launcher)).
   Para conferir antes: `bash scripts/check-launcher-version.sh`.
3. Suba `src/server/VERSION` (`1.2.0`, ou `1.2.0-beta.1` para pré-lançamento).
4. Leve tudo ao `master`.

O push que altera `src/server/VERSION` no `master` dispara o `release.yml`:

| Passo          | O que faz                                                                                                  | Se falhar                                        |
|----------------|------------------------------------------------------------------------------------------------------------|--------------------------------------------------|
| Versão         | Lê o arquivo. Se a tag `server-v<versão>` já existe, termina sem fazer nada. Confere a versão do launcher. | Nada foi publicado                               |
| Build e testes | As suítes do servidor, com PostgreSQL                                                                      | Nada foi publicado                               |
| Imagem         | Constrói e roda a fumaça (`scripts/smoke-image.sh`)                                                        | Nada foi publicado                               |
| Docker Hub     | Envia a imagem                                                                                             | Nada foi publicado                               |
| Tag e release  | Cria `server-v<versão>` e a release no GitHub                                                              | A imagem saiu, a tag não — rode de novo (abaixo) |

Um commit que não mexe em `src/server/VERSION` **não dispara nada**. Não existe
mais CI a cada push: os testes rodam uma vez, na publicação, e na sua máquina
antes disso (`./scripts/tc check`).

É a release estável mais nova `server-v*` que o painel consulta para avisar o
admin de que há atualização. Rascunho e pré-lançamento não contam.

### A fumaça

A fumaça sobe o container contra um PostgreSQL de verdade e confere o que só
quebra ali: as migrations aplicam, a página serve o runtime do Blazor, e as
colunas têm a largura que o domínio declara. Se ela falhar, nada é publicado.
Ela existe porque uma release já saiu com o `blazor.web.js` respondendo 404 — a
página pré-renderiza no servidor, então continuava parecendo saudável, e só um
diálogo que não abria denunciava.

Para rodar o mesmo na sua máquina:

```bash
./scripts/tc smoke-image
```

### Tags da imagem

Para a versão `1.2.0`:

- `1.2.0` — a versão exata
- `1.2` — acompanha os patches dessa linha
- `latest`

Um pré-lançamento (`1.3.0-beta.1`) recebe **só** a versão exata. Nem `latest`
nem a tag curta: quem pede "a versão atual" não está pedindo um beta.

## Disparo manual

**Actions → Publicar servidor → Run workflow**, escolhendo o ramo:

- **publicar desmarcado** (padrão): só build e testes, em qualquer ramo. É como
  se valida um ramo de trabalho no GitHub, já que nada roda sozinho.
- **publicar marcado, no `master`**: o fluxo inteiro. Serve para repetir uma
  publicação que falhou e foi corrigida por um commit que não mexeu no
  `VERSION` — esse commit, sozinho, não dispara nada. Continua protegido pela
  conferência da tag: versão já publicada não sai de novo.

Se a imagem foi enviada e a criação da tag falhou, repetir é seguro: a mesma
versão é enviada por cima, com o mesmo conteúdo, e a tag é criada.

## A versão dentro da imagem

O número de `src/server/VERSION` é lido pelo MSBuild (`src/server/Directory.Build.props`) e vira a versão do assembly,
sendo o que o
handshake devolve ao launcher em `serverVersion` e o que o painel mostra no
menu. Ninguém passa a versão por parâmetro: IDE, `Dockerfile` e workflow leem o
mesmo arquivo.

Para conferir após publicar:

```bash
curl -s https://seu-dominio/api/handshake | jq .serverVersion
```

## O launcher

O launcher viaja **dentro da imagem do servidor**. O `Dockerfile` compila o WPF (win-x64, self-contained — compila no
Linux, só não roda) e leva junto o `vpk`
na mesma versão do pacote `Velopack`. Quando o container sobe, o servidor
empacota o launcher em `${TCMINE_ROOT}/updates/launcher/{canal}/` com um
`server.json` contendo o `Server:PublicUrl` dele. Resultado:

- quem baixa o `Setup.exe` pela página pública instala um launcher que **já
  sabe o endereço** e pareia sozinho no primeiro uso — o jogador não digita
  nada (o endereço passa pelas mesmas regras do digitado: HTTPS, handshake);
- quem já tem o launcher recebe a atualização na abertura seguinte;
- subir uma imagem nova é publicar o launcher dela. Não há passo separado
  para esquecer.

O empacotamento leva alguns segundos e só acontece quando muda a versão ou o
endereço: um arranque igual ao anterior não faz nada (a marca fica em
`.tcmine-bundle`, na pasta do canal). Trocar o `PublicUrl` reempacota a mesma
versão — o instalador velho apontaria para o endereço velho.

### A versão: `src/launcher/VERSION`

Um arquivo, uma linha (`1.0.0`). É lido pelo MSBuild (`src/launcher/Directory.Build.props` → `Version` dos assemblies),
pelo
`Dockerfile`, pelo `release-launcher.ps1` e pela guarda da release. Ninguém
passa a versão por parâmetro.

**Mudou o launcher, suba o número.** O Velopack só oferece atualização a quem
tem versão MENOR: um launcher alterado com o mesmo número chega a quem instala
de novo e nunca a quem já o tem. Por isso o `release.yml` **falha logo no primeiro passo** se,
desde a tag `server-v*` anterior, algo mudou em `src/launcher/`, `src/shared/`,
`Directory.Packages.props` ou `Directory.Build.props` e o `VERSION` do launcher
não subiu (`scripts/check-launcher-version.sh`). Rode o script na sua máquina
antes de subir a versão do servidor; com `--warn` ele só avisa.

Os testes do launcher continuam sendo trabalho da IDE, no Windows (`TCMine.slnx`): a build da imagem compila o binário,
mas não abre janela nenhuma.

### Publicar à mão (opcional)

`scripts/release-launcher.ps1` continua existindo para testar um pacote na sua
máquina, ou para pôr no ar um launcher sem lançar imagem:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-launcher.ps1
```

Ele lê a versão do `VERSION`, roda `Launcher.Core.Tests` e
`Launcher.Architecture.Tests`, publica self-contained e empacota em
`releases/launcher/`. Copiar isso para `${TCMINE_ROOT}/updates/launcher/{canal}/`
é intencional à mão: a máquina onde você empacota não deveria ter credencial
de escrita na que serve jogadores. Após copiar, devolva a pasta ao usuário
do container — senão a próxima imagem não consegue publicar por cima:

```bash
sudo chown -R 1654:1654 ${TCMINE_ROOT}/updates
```

Um pacote manual com versão **maior** que a da imagem é respeitado — o
arranque não o sobrescreve (`NewerAlreadyPublished` no log). Mas ele não leva
`server.json`: o instalador dele pede o endereço na primeira abertura. Quando a
imagem seguinte trouxer versão igual ou maior, o feed volta a ser dela.

Para desligar o empacotamento no arranque (feed mantido só à mão):
`LauncherUpdates__PublishBundled=false`. `Server__FreezeLauncherUpdates=true`
também o suspende, como já suspendia o resto do feed.

O canal deriva do **protocolo**, não da versão do produto — hoje `win-x64-p2`,
lido de `Protocol.Current` em `src/shared/TCMine.Contracts/Protocol.cs`.

### Quando o protocolo sobe

Subir `Protocol.Current` muda o canal, e um launcher no canal antigo **deixa de
ser aceite no handshake** (o mínimo sobe junto). O feed dele não recebe mais
nada — um canal novo não é atualização do anterior —, então quem estava nele
reinstala pelo `Setup.exe` da página pública, que a imagem nova já publicou no
canal novo. Avise os jogadores antes de subir.
