# Lançar uma versão

O repositório abriga dois produtos, então a tag diz de qual se trata:

| Tag | O que dispara |
|---|---|
| `server-v0.2.0` | Constrói a imagem e publica no Docker Hub (`release-server.yml`) |

O launcher não tem tag própria: ele vai **dentro da imagem do servidor** e é
publicado no feed quando o container sobe — ver [O launcher](#o-launcher).

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

Dois caminhos, os dois valem:

```bash
git tag server-v0.1.0
git push origin server-v0.1.0
```

ou, na tela **Releases → Draft a new release** do GitHub, criar a tag
`server-v0.1.0` ali mesmo e publicar. No segundo caso a release já existe quando
o workflow termina, e ele só acerta as marcas (estável vira *latest*,
pré-lançamento vira *pre-release*) — o título e as notas que você escreveu
ficam. Antes ele tentava criá-la de novo e falhava com `Release.tag_name already
exists`, **depois** de a imagem já estar publicada: a cruz vermelha não queria
dizer que a versão não saiu.

É a release estável mais nova `server-v*` que o painel consulta para avisar o
admin de que há atualização. Rascunho e pré-lançamento não contam.

O workflow **não roda os testes de novo** — o `ci.yml` já os roda a cada push na
master, e uma tag só se cria em cima de um commit que já passou por lá. O que
ele faz é **subir a imagem de verdade** antes de publicar, porque isso os
testes não cobrem: já saiu release com o runtime do Blazor respondendo 404 sem
nada ficar vermelho na suíte.

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

## O launcher

O launcher viaja **dentro da imagem do servidor**. O `Dockerfile` compila o WPF
(win-x64, self-contained — compila no Linux, só não roda) e leva junto o `vpk`
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

Um arquivo, uma linha (`1.0.0`). É lido pelo MSBuild
(`src/launcher/Directory.Build.props` → `Version` dos assemblies), pelo
`Dockerfile`, pelo `release-launcher.ps1` e pela guarda da release. Ninguém
passa a versão por parâmetro.

**Mudou o launcher, suba o número.** O Velopack só oferece atualização a quem
tem versão MENOR: um launcher alterado com o mesmo número chega a quem instala
de novo e nunca a quem já o tem. Por isso o `release-server.yml` **falha** se,
desde a tag `server-v*` anterior, algo mudou em `src/launcher/`, `src/shared/`,
`Directory.Packages.props` ou `Directory.Build.props` e o `VERSION` não subiu
(`scripts/check-launcher-version.sh`). No CI de cada PR a mesma conferência só
**avisa** — o número pode subir em qualquer commit até a release.

Os testes do launcher continuam sendo trabalho da IDE, no Windows
(`TCMine.slnx`): o CI compila o binário, mas não abre janela nenhuma.

### Publicar à mão (opcional)

`scripts/release-launcher.ps1` continua existindo para testar um pacote na sua
máquina, ou para pôr no ar um launcher sem lançar imagem:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-launcher.ps1
```

Ele lê a versão do `VERSION`, roda `Launcher.Core.Tests` e
`Launcher.Architecture.Tests`, publica self-contained e empacota em
`releases/launcher/`. Copiar isso para `${TCMINE_ROOT}/updates/launcher/{canal}/`
é de propósito à mão: a máquina onde você empacota não deveria ter credencial
de escrita na que serve jogadores. Depois de copiar, devolva a pasta ao usuário
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
