# Lançar uma versão

O repositório abriga dois produtos, então a tag diz de qual se trata:

| Tag | O que dispara |
|---|---|
| `server-v0.2.0` | Constrói a imagem e publica no Docker Hub (`release-server.yml`) |

O launcher não tem workflow: publicá-lo é um passo manual, feito na sua
máquina Windows — ver [Lançar o launcher](#lançar-o-launcher) abaixo.

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

## Lançar o launcher

Não há workflow: o launcher é Windows (WPF + WebView2) e compilar/empacotar só
faz sentido na sua própria máquina — é por isso que ele saiu do GitHub Actions
(nem o `ci.yml` verifica mais o launcher a cada push; isso agora é trabalho da
IDE, rodando `TCMine.slnx` e a suíte de testes localmente antes de publicar).

```powershell
./scripts/release-launcher.ps1 -Version 0.2.0
```

Rode do terminal — inclusive o embutido no Rider. O script:

1. Confere se o SDK do `global.json` resolve (e corrige o `PATH` sozinho se ele
   só existir em `%USERPROFILE%\.dotnet`, em vez de morrer com "SDK not found").
2. Roda `TCMine.Launcher.Core.Tests` e `TCMine.Launcher.Architecture.Tests` —
   só o que o launcher toca, não a suíte inteira do servidor.
3. Publica self-contained para win-x64. Self-contained porque o jogador não
   deve instalar runtime nenhum, e o Velopack substitui a pasta inteira a cada
   update — meio caminho dependente do runtime seria mais uma coisa para dar
   errado na máquina de outra pessoa.
4. Instala/atualiza o `vpk` global na MESMA versão do pacote `Velopack`
   referenciado pelo launcher (`Directory.Packages.props`) — um `vpk`
   desalinhado pode empacotar num formato que a biblioteca embutida não
   entende, e o sintoma só aparece no autoupdate de quem já instalou.
5. Empacota com o Velopack em `releases/launcher/`.

O canal deriva do **protocolo**, não da versão do produto — hoje `win-x64-p2`,
lido de `Protocol.Current` em `src/shared/TCMine.Contracts/Protocol.cs`. É o
que permite publicar launcher 1.6, 1.7 e 1.8 sem release nenhuma do servidor;
o script lê o número de lá em vez de repeti-lo, porque dois lugares discordando
publicam no canal errado, entregando uma atualização que o servidor nunca vai
oferecer.

Use `-SkipTests` só para reexecuções rápidas sobre código já testado — não é o
caminho normal. E abra o launcher publicado pelo menos uma vez antes de
distribuir: um launcher publicado é tão imutável quanto uma imagem, porque
alguém pode já ter instalado no minuto seguinte.

### Configurar como Run Configuration no Rider

`Run` → `Edit Configurations…` → `+` → `Shell Script` → aponte **Script path**
para `scripts/release-launcher.ps1`, **Interpreter path** para `powershell.exe`,
em **Interpreter options** ponha `-ExecutionPolicy Bypass` e em **Script
options** passe `-Version 0.2.0` (troque a versão a cada release). Fica salvo
em `.idea/`, que é local e não versionado — cada máquina configura a própria.

O `-ExecutionPolicy Bypass` é necessário porque a política padrão do Windows
recusa rodar `.ps1` sem estar assinado. É por invocação, não altera nada no
sistema — a alternativa seria `Set-ExecutionPolicy -Scope CurrentUser
RemoteSigned` uma vez só (afeta só o seu usuário), mas isso exige decidir mexer
numa configuração do Windows, e a Run Configuration resolve sem isso.

Pelo terminal (fora do Rider) é o mesmo parâmetro:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-launcher.ps1 -Version 0.2.0
```

### Pôr a release no ar

Copie o conteúdo de `releases/launcher/` para o servidor — de propósito à mão,
e não por um pipeline: a máquina onde você empacota não deveria ter credencial
de escrita na que serve jogadores.

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
