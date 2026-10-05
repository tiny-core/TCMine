# Changelog

Todas as mudanças relevantes do **TCMine Server** e, a partir da 0.4.0, também
do **TCMine Launcher**, que partilham repositório e protocolo. Desde a 1.0.0, o
launcher vai dentro da imagem do servidor e a tag é uma só
(`server-v*`); o número dele vive em `src/launcher/VERSION`. As versões
seguem [SemVer](https://semver.org/lang/pt-BR/). A partir da **1.0.0**, o que
está em [Compromisso de estabilidade](README.md#compromisso-de-estabilidade) só
muda de forma incompatível numa versão MAIOR.

O texto completo de cada lançamento está na
[página de releases](https://github.com/tiny-core/TCMine/releases).

## [Não lançado]

Launcher **1.1.0** (vai na próxima imagem do servidor).

### Adicionado

- **Comandos pelo console do painel.** O dono e os admins do servidor (e
  moderadores, dentro da lista permitida) mandam comandos pelo RCON direto da
  tela do servidor: Enter envia, ↑ e ↓ percorrem o histórico, e a sintaxe do
  comando aparece enquanto se digita. O botão *Comandos* abre uma referência
  pesquisável dos comandos do Minecraft (jogadores, moderação, mundo, itens,
  servidor), com os perigosos marcados; um clique coloca o comando na caixa. O
  `stop` pelo console é recusado: com o restart automático do container o
  servidor voltaria sozinho — parar é pelo botão Parar.

- **Entrar no servidor com um clique.** A lista de servidores da tela de jogar
  ganhou o botão *Entrar*: abre o Minecraft já conectando ao servidor
  (`--quickPlayMultiplayer`). Se o servidor estiver numa versão do modpack mais
  nova que a da instância, ela é atualizada antes, com o backup automático do
  mundo; se estiver numa mais antiga, o launcher recusa e explica — descer de
  versão quebra o mundo. Servidor parado ou jogo já aberto deixam o botão
  desligado, com o motivo.

- **Ações travadas com o jogo aberto.** Instalar, atualizar, remover, trocar
  a RAM, limpar Java e sair da conta ficam desligados até o jogo fechar — com o
  motivo no botão e um aviso em todas as telas, que também oferece fechar o
  jogo.

### Mudado

- **Visual novo: "pedra vulcânica".** Painel, página pública e launcher passam
  a seguir o TCMine Design System — fundo de pedra escura, laranja só como luz
  (ação primária, foco, estado), sem azul. **Só tema escuro**: o alternador de
  tema do painel saiu. Botões em minúsculas ("Novo modpack"), cartões com
  borda fina, foco sempre visível.
- **Menu do painel agrupado** em Conteúdo, Jogo, Pessoas e Sistema, com link
  para a página pública no topo.
- **Visão geral reformulada.** Jogadores online, servidores no ar, modpacks e
  disco no topo; cada servidor com CPU, RAM e jogadores; um quadro "Precisa da
  sua atenção" (servidor que caiu, disco quase cheio, pedidos de acesso,
  versão que falhou); tabela dos modpacks com a versão publicada e o
  rascunho. Só números medidos — onde não há medida, um traço.
- **Página pública reformulada.** Download do launcher em destaque, "Como
  jogar" em três passos, cartões de modpacks e servidores (lotação, sem
  endereço) e uma chamada final; funciona na largura de um celular.
- **Tela de Armazenamento no visual novo**: `PageHeader` em vez de
  cabeçalho manual, painéis com a classe `tc-panel`, e a barra de ocupação
  do content store retintada para os tokens `--tc-*` (estava em
  `--mud-palette-*`, único resquício nessa tela).
- **Tela de Usuários no visual novo**, com a tabela dentro do mesmo painel
  de borda fina das demais listas. Os chips de papel do diálogo "Ver
  vínculos" ("Membro"/"Moderador"/"Admin"/"Dono"/"Editor") foram para
  minúsculas.
- **Nuvem de itens no visual novo.** A lista de nuvens (`/admin/cloud`) ganhou
  o painel de borda fina e o estado vazio com `EmptyState` (era um
  `MudAlert` solto, sem o botão de criar à mão). Chips capitalizados que
  escaparam da regra ("Ligada"/"Ligado"/"Em outra nuvem"/"Bloquear"/
  "Permitir", entre a lista de nuvens e os painéis de servidores e regras)
  foram para minúsculas.
- **Tela de Mods (inventário) no visual novo**, com o mesmo `PageHeader` e
  painel de borda fina das demais. O selo de origem (Modrinth/CurseForge/
  envio manual) ganhou um componente próprio, `OriginChip` — era um switch
  de ícone/cor repetido entre esta tela e a aba de mods da versão, e nenhum
  dos dois traduzia "ManualUpload" ("upload manual" saía cru, em inglês,
  sem espaço). A célula "Lado" da aba de mods tinha o mesmo problema fora
  do rascunho ("Both"/"ClientOnly") — corrigido junto, num `FileSideLabels`
  compartilhado com o aviso de pendências que já existia.
- **Tela de Servidores (painel) no visual novo.** Cabeçalho padrão
  (`PageHeader`, como a Visão geral já usa) em vez de um `MudStack` manual
  próprio; painel com borda fina. Os selos de status de servidor e de
  versão (`ServerStatusChip`, `VersionStateChip`) passam a minúsculas
  ("online", "rascunho"…) — eram os dois únicos componentes compartilhados
  que ainda escapavam da regra "chips em minúsculas", e a correção vale
  para toda tela que os usa (também a aba Servidores do modpack e a tela
  de jogar do launcher).
- **Tela de jogar do launcher, com "Atualizar e entrar" e lotação.** O
  cartão da instância ativa ganhou o fundo em gradiente dos tokens
  `--tc-*` (antes em `--mud-palette-*`). Cada servidor da lista mostra
  jogadores online/máximo (não é dado sensível como o endereço, que
  continua só para quem tem acesso), e o botão passa a dizer "Atualizar e
  entrar" quando a versão do servidor está na frente da instância
  instalada — comparação por SemVer (`ModpackVersionOrder`), feita no
  cliente a partir de um campo novo e opcional no contrato
  (`GameServerDto.ModpackVersionLabel`, resolvido no hub por modpack, sem
  consulta por servidor). Servidor atrás da instância aparece desligado
  com o motivo, antes do clique — `JoinServer` já recusava, só que depois.
- **Aba de mods e cabeçalho de versão no visual novo.** O seletor de versão
  mostra o selo de estado (rascunho/resolvendo/publicado/arquivado) em vez do
  nome do enum, e o stepper do ciclo de vida segue a mesma paleta das outras
  telas. "Procurar mods" (antes "Buscar no Modrinth" — a busca já cobre o
  CurseForge também) e "Enviar arquivo" migraram para a barra da própria
  grade, ao lado do filtro; um indicador "Tudo salvo"/"Salvando…" acompanha a
  troca de lado por célula. Mods pendentes de upload manual aparecem num
  aviso no topo da aba (reaproveitando o painel que já existia na Visão
  geral). "Nova versão" e "Publicar" passam a ficar à mão em qualquer aba por
  versão (Mods, Recursos, Overrides), não só na Visão geral — a lógica de
  confirmação e publicação foi extraída para `VersionLifecycleActions`,
  compartilhada entre as duas.

### Corrigido

- **O botão de procurar mods não aparecia numa versão nova** (nem ao trocar a
  versão no seletor) até recarregar a página. Navegar para a mesma aba com
  outro id reaproveita o componente, e os dados só eram carregados na primeira
  vez; o mesmo acontecia nas abas de detalhe, novidades e servidores ao trocar
  de modpack. Um teste agora exige que toda página com id na rota recarregue
  quando ele muda.
- **A página pública mostrava o endereço (IP) dos servidores** a qualquer
  visitante. Ele só sai para quem tem acesso aprovado, e o modelo público nem
  o carrega mais.

### Melhorado

- **Downloads em paralelo.** O launcher baixava um arquivo de cada vez, e um
  pack grande (ATM10: centenas de mods, milhares de configs) pagava a latência
  de cada pedido em série — lento até em rede local. Agora são seis
  simultâneos (o servidor aceita oito por cliente), os maiores primeiro, e o
  mesmo conteúdo em dois caminhos é baixado uma vez só.

## [1.0.1] — 2026-10-05

### Corrigido

- **O launcher embutido não era publicado quando o feed tinha arquivos de uma
  publicação manual copiados como root.** O `vpk` só descobria no último passo
  e o log mostrava apenas o stack trace dele (`vpk saiu com 255`). Agora a
  permissão é conferida antes, e o erro diz qual arquivo e o comando que
  resolve (`sudo chown -R 1654:1654 ${TCMINE_ROOT}/updates`); qualquer outra
  falha do `vpk` chega ao log pela linha de erro dele, não pelo stack trace.

- **Um proxy ou CDN podia continuar a entregar o instalador antigo.** O
  `Setup.exe` e os índices do feed têm nome fixo e eram servidos sem
  `Cache-Control`; agora vão com `no-cache` (os `.nupkg`, que levam a versão no
  nome, ficam em cache), e o link da página pública leva `?v={versão}`.

## [1.0.0] — 2026-10-05

A primeira versão estável. Fecha os defeitos que faziam o servidor de jogo
subir na versão errada e o mesmo jogador virar duas contas, e passa a tratar
a ingestão de mods com o que já está em casa antes de ir à rede.

### Adicionado

- **Escolher a versão na importação.** Ao importar um modpack, a lista de
  releases aparece com a estável mais recente marcada; ao adicionar mods pela
  busca, cada mod marcado ganha um seletor com as releases compatíveis com o
  Minecraft e o loader do pack.

- **Ingestão "banco → disco → rede".** Antes de consultar o Modrinth ou o
  CurseForge, o TCMine verifica se aquela release já foi ingerida (em qualquer
  modpack) e se os bytes estão no disco. Importar um segundo pack que partilha
  metade dos mods com o primeiro deixa de baixar e de gastar cota de API com
  essa metade. A importação também passou a recusar um pack já importado
  **antes** de baixar o zip.

- **Aviso de atualização no painel.** O admin da instalação vê quando sai uma
  versão estável nova do TCMine Server, com o link das notas e o comando para
  atualizar. A consulta vai às releases `server-v*` do GitHub, com cache de seis
  horas; `Updates__Enabled=false` desliga.

- **Reaver a administração** (`/admin/claim`). Se nenhum administrador consegue
  entrar pela Microsoft — o caso de quem só tinha conta de e-mail e senha antes
  da 0.5.0 —, o servidor escreve no log, ao arrancar, um código de uso único.
  Quem opera a máquina entra com a Microsoft, informa o código e vira o
  administrador; havendo uma única conta de admin antiga, ela é unida à nova,
  com modpacks, servidores e acessos.

- **O launcher vai dentro da imagem do servidor.** O `Dockerfile` compila o
  launcher e leva o `vpk`; ao subir, o servidor o empacota em
  `updates/launcher/{canal}/` — só quando a versão ou o endereço mudam. Lançar
  o servidor passa a ser lançar o launcher dele, sem passo manual para
  esquecer. A imagem cresce para ~1 GB. `LauncherUpdates__PublishBundled=false`
  desliga; um pacote publicado à mão com versão maior é respeitado.

- **O launcher já conhece o servidor.** O instalador gerado pelo servidor leva
  um `server.json` com o `Server:PublicUrl`, e o launcher pareia sozinho no
  primeiro uso — o jogador não digita endereço. Passa pelas mesmas regras do
  endereço digitado (HTTPS, handshake); falhando, a tela de pareamento abre
  com ele preenchido.

- **`src/launcher/VERSION`** é a única fonte da versão do launcher: MSBuild,
  `Dockerfile` e `release-launcher.ps1` leem dela (o script perdeu o
  `-Version`). A release do servidor **falha** se o launcher mudou desde a tag
  anterior e o número não subiu; o CI de PR avisa.

### Removido

- **E-mail.** O SMTP, o servidor de e-mail próprio (container
  `tcmine-mail`) e a aba *E-mail* das Configurações serviam à recuperação de
  senha, que deixou de existir com o login só pela Microsoft (0.5.0). As
  colunas saem do banco na migration `RemoveEmail`.

- O workflow `release-launcher.yml`, que ainda disparava em tags `launcher-v*`
  embora o launcher seja publicado à mão (ver
  [docs/RELEASE.md](docs/RELEASE.md#o-launcher)).

### Corrigido

- O link de download do launcher na página pública nunca aparecia: o servidor
  lia o feed do Velopack num formato que ele não tem e não achava o instalador.

- **O servidor de jogo não subia na versão do modpack.** O container era criado
  uma vez e reaproveitado para sempre: trocar a versão do servidor (ou a
  memória) não chegava ao jogo, nem os mods eram rematerializados. Agora o
  container é recriado sempre que a configuração muda — o mundo vive na pasta
  montada e não se perde. Em Fabric e Quilt, a build do loader ia numa variável
  que a imagem ignora, e o servidor subia com o loader mais recente.

- **A versão fixada de um mod podia virar a mais recente.** Quando a release
  pedida não aparecia na primeira página da origem (50 arquivos no CurseForge),
  o TCMine instalava a mais nova sem avisar. Releases fixadas agora são buscadas
  pelo id. O mesmo valia para releases antigas de um pack no CurseForge.

- **Jogador duplicado.** Dois logins simultâneos no primeiro arranque do
  launcher podiam terminar em erro; e quem entrou no painel antes de ter o
  Minecraft vinculado ganhava uma segunda conta ao entrar pelo launcher. A
  corrida agora adota a conta vencedora, e a duplicata é fundida na conta do
  painel no próximo login dela (ou ao vincular o Minecraft), mantendo todos os
  acessos.

- **Mod do Modrinth em dobro.** A busca gravava o mod pelo slug, e o pack
  importado e as dependências pelo id do projeto: o mesmo mod pelos dois
  caminhos virava dois `.jar` em `mods/`. A identidade agora é sempre o id, e a
  próxima atualização de um mod gravado pelo slug substitui a linha antiga.

- **A publicação da imagem ficava vermelha quando a release era criada pela
  tela do GitHub.** A imagem saía (foi o que aconteceu na 0.4.0 e na 0.5.0), mas
  o último passo tentava criar uma release que já existia. Agora ele só acerta
  as marcas da release existente.

- Clonar uma versão ou criar uma a partir de outra perdia o ícone dos mods.

### Manutenção

- .NET 10.0.12 (servicing) e as actions do GitHub nas versões que rodam em
  Node 24 — as anteriores rodavam em Node 20, descontinuado nos runners.

### Atualizar

Nada a fazer no banco: as migrations novas (`AddModpackFileDependencies`,
`RemoveEmail`) aplicam-se no arranque. O protocolo do launcher **não** mudou
(continua 2): não é preciso publicar launcher novo.

Os containers dos servidores de jogo existentes são **recriados no próximo
start** — é a primeira vez que levam a impressão digital da configuração. O
mundo fica: ele vive na pasta da instância, não no container.

Se o servidor de e-mail próprio estava ligado, o container `tcmine-mail` fica
órfão: remova-o com `docker rm -f tcmine-mail` (e, se quiser, a pasta
`{TCMINE_ROOT}/instances/mail`, onde ele guardava o estado).

Quem vem de uma instalação com contas de e-mail e senha e não consegue mais
administrar: procure no log do arranque a linha com o código de resgate
(`docker compose logs tcmine | grep /admin/claim`), abra `/admin/claim` e
informe-o.

## [0.5.0] — 2026-10-05

### Adicionado

- **Login só pela Microsoft.** Acabou o par e-mail/senha: o primeiro acesso
  (`/admin/setup`) pede o client ID do Entra ID e quem entra primeiro vira o
  administrador da instalação. O painel e o launcher passam a ser a mesma conta
  — antes o servidor recém-criado do admin não aparecia no launcher dele, porque
  eram duas contas sem relação.

- **Pedidos de acesso.** O launcher mostra também os servidores com whitelist em
  que o jogador ainda não entrou, com o endereço escondido e um botão "Pedir
  acesso"; o admin aprova ou recusa na página *Pedidos de acesso*. Servidor
  **sem** whitelist aparece para qualquer jogador autenticado, sem convite.

- **Convites pelo launcher.** O jogador resgata o código que recebeu direto na
  tela inicial.

- **Página de Usuários** (só para o admin da instalação): buscar contas,
  promover ou rebaixar administradores (sem nunca ficar sem nenhum) e ver os
  acessos de cada um.

- **Nuvem de itens (TC Cloud Storage)**, para o mod `tccloud`: cofres por dono,
  API para os servidores de jogo com chave renovada a cada start, ledger
  append-only, quarentena, detecção de mundo que voltou no tempo e painel de
  governança. O plano e as decisões estão em
  [docs/CLOUD-STORAGE.md](docs/CLOUD-STORAGE.md).

### Corrigido

- A primeira versão de um modpack é sugerida como `1.0.0-alpha` (era
  `1.0.1-alpha`, um número sem nada antes dele), e criar uma versão sem herdar
  mods explica que só o rascunho novo nasce vazio.
- A tela de novo servidor mostrava a build do loader no lugar da versão do
  Minecraft.
- O processo do launcher podia ficar vivo depois de fechar a janela.

### Atualizar

As contas de e-mail e senha deixam de existir: entra-se pela Microsoft. Uma
conta antiga só é reconhecida se tinha um Minecraft vinculado; sem isso, o
caminho de volta à administração chegou na 1.0.0 (`/admin/claim`).

## [0.4.0] — 2026-09-28

A versão em que o launcher deixou de ser promessa. O laço fecha: parear, entrar
com a conta Microsoft, instalar um modpack e **abrir o jogo**.

### Adicionado — launcher

- **Login com a Microsoft**, pela cadeia completa (Microsoft → Xbox Live → XSTS →
  Minecraft). Usa o broker do Windows quando existe e o navegador do sistema
  quando não — nunca uma WebView embutida, que é a diferença entre ver a barra de
  endereço da Microsoft e escrever a palavra-passe numa janela que qualquer um
  podia ter desenhado.

- **Abrir o jogo**, com o Java que cada versão pede descarregado e partilhado
  entre instâncias, e o loader instalado (NeoForge, Fabric, Quilt, Forge). O
  registo do jogo aparece na tela, então um crash deixa de ser "fechou sozinho".

- **Instâncias com identidade própria.** Atualizar mantém o mundo, as
  configurações e a RAM escolhida; e o mesmo pack pode ter duas instalações, cada
  uma no seu mundo — o que faz falta a quem joga em servidores fixados em versões
  diferentes.

- **Cópia do mundo antes de cada atualização**, automática quando há mundo. Se a
  cópia falhar, a atualização é cancelada: é a ordem que torna a operação
  reversível. Guarda as cinco mais recentes por instância.

- **Canal alpha.** Uma instância alpha acompanha pré-lançamentos e nunca salta
  para o canal estável, nem o contrário. Escolher versão é coisa da instalação:
  uma instalação existente só anda para a frente, porque descer parte os mundos
  jogados.

- **Funciona sem o servidor no ar.** O que está no disco continua jogável; só o
  catálogo e as novidades ficam de fora, desligados com o motivo à vista.

- **Atualiza-se sozinho**, por Velopack, pelo feed que o servidor publica.

### Adicionado — servidor

- **Feed de atualização do launcher** em `/updates/launcher/{canal}/`, servido de
  `LauncherUpdates:RootPath` (derivado de `Storage:RootPath`). Anónimo por
  necessidade: um launcher velho demais para autenticar é exatamente o que
  precisa de se atualizar.

- **Histórico de versões e novidades pelo hub**, para o seletor de versão e a
  tela inicial do launcher. Os dois filtram do lado do servidor — rascunho e
  pré-lançamento não saem por engano.

- **O client ID do Azure mudou-se para a tela de Configurações.** Registar a app
  no Entra ID acontece depois do deploy, e antes disso o valor só existia em
  `appsettings` — o administrador tinha de editar JSON dentro do container e
  reiniciar. `Server:AzureClientId` sobrevive como semente.

- **Modpacks com dono e editores**, como os servidores já tinham.

- **Página pública de catálogo** para quem visita sem login.

### Corrigido

- A retenção de backups de mundo do **painel** nunca era gravada: o caso de uso
  punha o valor na entidade e o repositório descartava-o, então ela voltava a
  cinco a cada "Salvar".

### Atualizar

O **protocolo subiu para 2**, e o mínimo aceite subiu junto: um launcher de
protocolo 1 é recusado no handshake e mandado atualizar. Como o canal do Velopack
deriva do protocolo, **publique o launcher antes de subir o servidor** — senão o
jogador recebe uma instrução que não tem como cumprir.

## [0.3.0] — 2026-08-23

### Adicionado

- **"Só quem tem convite entra"**, um interruptor por servidor, ligado por
  padrão. O TCMine passa a manter a whitelist do servidor de jogo com os membros
  do painel: convite resgatado entra, membro removido sai, e a lista é reescrita
  toda vez que o servidor sobe.

  Vale explicar por que é whitelist e não senha: **o Minecraft não tem senha de
  entrada**. A lista de servidores do cliente guarda endereço e nome, e o
  protocolo não prevê credencial nenhuma. A whitelist é o mecanismo que o jogo
  oferece — e o TCMine sabe quem convidar porque o jogador entra com perfil
  Minecraft verificado.

  Uma ressalva honesta: a whitelist prende à **conta**, não ao launcher. Um
  convidado que já tenha os mods poderia entrar por fora. Com um pack de
  centenas de mods isso é teórico, mas não é a mesma promessa.

  Um membro que ainda não entrou no jogo não é adicionado — não há nome de
  jogador para pôr na lista. Ele entra sozinho no primeiro login.

### Corrigido

- **A aba de Recursos ficava carregando para sempre.** A página passava um
  parâmetro que o componente não tem; o Razor compila, e a página estoura ao
  renderizar. Dentro de uma grade isso vira um spinner eterno, porque o
  componente de grade não tem estado de erro.

  O caso da lista vazia era o que escondia: aquele trecho só é construído quando
  não há linhas. Agora um teste renderiza todas as abas de versão, cheias e
  vazias, e reprova se alguma der erro.

### Atualizar

```bash
docker compose pull && docker compose up -d --force-recreate
```

Servidores existentes nascem com a whitelist **ligada**. Suba cada um uma vez
para que a lista seja escrita a partir dos membros — ou desligue o interruptor
no servidor que você quiser deixar aberto.

## [0.2.0] — 2026-08-23

Esta versão nasceu de um servidor de jogo que não subia. Perseguir a causa
revelou três problemas distintos, e consertá-los abriu espaço para o resto.

### Corrigido

- **Servidores de jogo não iniciavam, e a tela não dizia por quê.** Eram duas
  coisas somadas. O painel roda como um usuário sem privilégio e precisa
  pertencer ao grupo do socket do Docker; fora dele, toda operação de container
  falha com *permission denied* — e o erro morria num aviso de tela que sumia
  com a página, sem deixar nada no log. Agora o arranque avisa (`Sem acesso ao
  Docker`) com o comando para descobrir o número certo, e as falhas de iniciar,
  parar e restaurar mundo ficam registradas.

  Ver a seção nova em [docs/DEPLOY.md](docs/DEPLOY.md).

- **Apagar um servidor falhava com "acesso negado".** O container do jogo
  assumia a posse da pasta da instância, e o painel deixava de conseguir
  removê-la. Os dois passam a usar o mesmo usuário.

- **Mods de cliente iam para o servidor.** Um pack CurseForge não declara em que
  lado cada mod roda, e o `neoforge.mods.toml` também não — então tudo entrava
  como "os dois", e o servidor morria no arranque pedindo uma dependência que é
  de cliente. Três saídas, nessa ordem: o **server pack do autor** decide (é a
  lista curada do que um servidor precisa), o **jar** decide quando declara
  (Fabric), e o **admin** decide sempre, num seletor na grade de mods.

- **Shaderpacks apareciam como "sem versão compatível".** A busca filtrava todo
  projeto por loader, e shaderpack não tem loader. Cada arquivo vai agora para a
  pasta da sua categoria em vez de todos para `mods/`, onde um `.zip` derrubaria
  o jogo.

- **Verificar atualizações podia ser disparado sem limite**, empilhando
  varreduras iguais contra a mesma cota de API.

- **O editor de código não carregava** (quebrou na 0.1.4).

### Adicionado

- **Puxar mods pendentes do server pack do autor.** No CurseForge o autor pode
  proibir o download avulso do `.jar` — a causa da maioria das pendências — e ao
  mesmo tempo publicar um server pack com esses arquivos dentro. Um rascunho com
  pendências oferece o botão; ele preenche o que dá e conserta os lados na mesma
  passada.
- **Cancelar um trabalho em curso.** Importações e resoluções de vinte minutos
  não tinham saída além de esperar ou reiniciar. O cancelamento devolve a versão
  ao rascunho e mantém o que já foi baixado.
- **Aba de Recursos** para shaderpacks e resource packs, com envio direto.
- **A versão do TCMine Server no rodapé do menu.**
- **Aviso de servidor com versão defasada**, com a versão publicada mais recente
  ao lado.
- **A versão do loader é editável** enquanto a versão é rascunho.
- O selo de origem nas listas leva à página do projeto.

### Melhorado

- Reingerir um mod que não mudou não o baixa de novo. Numa reimportação de um
  pack grande, é um gigabyte e meio a menos.
- O painel de pendências mostra o tipo e o lado de cada arquivo, virou um bloco
  recolhível, e não aparece mais durante a resolução — quando todos os mods do
  pack estão, por definição, pendentes.

### Atualizar

```bash
docker compose pull && docker compose up -d --force-recreate
```

Se algum servidor de jogo já existia, a pasta dele ficou com o dono errado.
Corrija uma vez, no host:

```bash
sudo chown -R 1654:1654 /caminho/do/tcmine/instances
```

## [0.1.7] — 2026-08-23

### Corrigido

- **Shaderpacks de um modpack apareciam como "sem versão compatível".** Nem tudo
  o que um pack lista é mod: o All the Mods 10 traz 481 mods e 4 shaderpacks no
  mesmo manifesto. A busca filtrava todo projeto por loader, e shaderpack não tem
  loader — é lido pelo Iris, não pelo NeoForge —, então a consulta voltava vazia
  para arquivos que estavam lá o tempo todo.

  Atrás dessa falha havia outra pior: todo arquivo resolvido era gravado em
  `mods/`, e um `.zip` de shader ali derruba o jogo no arranque. Agora cada
  arquivo vai para a pasta da sua categoria — `shaderpacks/`, `resourcepacks/`,
  `datapacks/`.

  Shaderpacks e resource packs também passam a ser marcados como de cliente, o
  que os mantém fora do container do servidor.

  Num pack já importado essas pendências não somem sozinhas: use "Tentar de
  novo" no painel de pendências.

### Atualizar

```bash
docker compose pull && docker compose up -d --force-recreate
```

## [0.1.6] — 2026-08-23

### Adicionado

- **Mods pendentes agora saem do server pack do autor.** Um rascunho com
  pendências oferece "Puxar do server pack": o TCMine baixa o zip que o autor
  publica e tira dele os `.jar` que faltam. É a saída para a pendência mais
  comum — o autor proíbe que terceiros baixem o arquivo avulso, mas publica um
  server pack com ele dentro. Um mod que não esteja no zip continua pendente, e
  o resultado diz quantos sobraram.

  Só em rascunho. Acrescentar arquivos a uma versão publicada mudaria o que já
  foi prometido a quem instalou.

  Versões importadas antes desta atualização descobrem o server pack sozinhas no
  primeiro arranque — não é preciso reimportar nada.
- **A versão do TCMine Server aparece no rodapé do menu.** É a pergunta que
  surge quando algo está errado, e ela não devia estar escondida atrás de uma
  tela de "sobre".
- **Servidor com versão defasada é marcado na lista**, com a versão publicada
  mais recente ao lado. Fixar a versão no servidor é de propósito — é o que
  permite atualizar um de cada vez e voltar atrás —, mas nada dizia que havia
  ficado para trás.
- **A versão do loader pode ser editada enquanto a versão é rascunho.**

### Corrigido

- **O editor de código voltou a carregar.** Ele quebrou na 0.1.4: a lista de
  scripts chegava ao navegador como argumentos soltos, e o carregador pedia ao
  servidor um arquivo chamado `_`.
- **O painel de mods pendentes não aparece mais durante a resolução.** Enquanto
  a ingestão corre, todos os mods do pack estão na fila — e o painel anunciava o
  pack inteiro como "aguardando upload manual".
- **Mods sem versão compatível mostravam o código do projeto**, sem nome nem
  link. Agora trazem os dois.
- O painel de pendências virou um bloco recolhível com tabela: uma dúzia de
  linhas abertas empurrava as estatísticas da versão para fora da tela.

### Atualizar

```bash
docker compose pull && docker compose up -d --force-recreate
```

## [0.1.5] — 2026-08-22

### Adicionado

- **A versão do loader agora pode ser editada enquanto a versão é rascunho.**
  Ela pertence à versão, e não ao modpack, justamente porque sobe entre versões
  — mas só podia ser definida na criação. Errar o número significava apagar o
  rascunho e recomeçar, com os mods e overrides já dentro dele. Numa versão
  publicada continua imutável: ela faz parte do que foi prometido, e mudá-la
  deixaria quem já instalou rodando contra outro loader.

### Corrigido

- **Um arquivo órfão que não podia ser selecionado não dizia por quê.** Arquivos
  gravados nas últimas 24 h ficam de fora de propósito — a ingestão grava os
  blobs e as linhas que os referenciam em lotes separados, então apagar um blob
  recente quebraria uma importação em curso. A regra estava no aviso acima da
  tabela, mas não no controle que ela desabilita, e um checkbox que não marca
  parece defeito.

### Documentação

- **Instalações atrás do Cloudflare**: o Bot Fight Mode injeta um script inline
  em toda página, a CSP do painel o bloqueia, e o console mostra um erro que
  parece da aplicação. Não é, e o painel não é afetado. O `docs/DEPLOY.md`
  explica como reconhecer (o hash sugerido muda a cada carregamento, porque o
  script carrega o `CF-RAY` da resposta), como confirmar por linha de comando, e
  por que a saída é desligar a opção no Cloudflare em vez de afrouxar a CSP.

### Interno

- Um teste passa a buscar cada página do painel e reprovar se alguma servir
  JavaScript inline. A CSP `script-src 'self'` depende disso, e até agora a
  garantia era um comentário no código.

## [0.1.4] — 2026-08-22

### Corrigido

- **O painel que explica os mods pendentes nunca aparecia.** Ao publicar um pack
  recém-importado, o servidor avisava que N mods estavam pendentes de upload
  manual — e a página não mostrava mais nada: nem quais, nem por quê, nem o que
  fazer. O painel que responde às três perguntas já existia e simplesmente não
  era renderizado.

  Vale explicar o aviso, que não é defeito: no CurseForge o autor pode proibir
  que terceiros baixem o arquivo do mod. Não há saída técnica legítima — os
  launchers oficiais fazem o mesmo, abrem a página do mod para o jogador baixar.
  Por isso a pendência é registrada em vez de reprovar a versão: um pack grande
  ficaria impublicável para sempre por causa de meia dúzia deles.

### Melhorado

- **O editor de código não é mais baixado em toda página.** Ele estava
  declarado globalmente, então uma página vazia puxava treze arquivos dele sem
  ter editor nenhum. Agora desce só na aba de Overrides, que é a única que o
  usa.

### Atualizar

```bash
docker compose pull && docker compose up -d --force-recreate
```

## [0.1.3] — 2026-08-22

### Corrigido

- **A resolução de um pack importado travava no fim**, com o modpack parado em
  "Resolvendo" depois de já ter baixado quase todos os mods. Ao enfileirar, o
  servidor registra uma pendência para cada mod do pack; ao terminar, troca a
  razão das que não deram certo (autor não permite redistribuir, sem arquivo
  compatível). Essa troca criava um registro novo em vez de atualizar o
  existente, e o banco recusava a duplicata — derrubando a gravação final e
  levando junto o resultado de toda a ingestão.

  No All the Mods 10 isso significava baixar 473 mods e perder o trabalho por
  causa dos 8 restantes.

### Atualizar

```bash
docker compose pull && docker compose up -d --force-recreate
```

Uma versão que ficou presa em "Resolvendo" é retomada sozinha no arranque (até
três tentativas). Se a sua já esgotou as tentativas, ela aparece como falha —
devolva ao rascunho e mande resolver de novo.

## [0.1.2] — 2026-08-22

### Corrigido

- **Importar packs grandes do CurseForge ainda falhava**, com o mesmo
  `value too long for type character varying(512)` da 0.1.1 — em outra coluna. O
  registro do que veio da origem guarda um par projeto/arquivo e o nome de
  **cada** mod do pack, então um pack de trezentos mods gera dezenas de KB. A
  configuração dizia que essa coluna não tinha limite; não tinha efeito, e ela
  saía com os 512 do padrão. Agora é `text`, sem limite de verdade.
- As colunas que guardam **por que** um mod ficou pendente foram alargadas: uma
  mensagem de erro longa derrubava a ingestão justamente ao registrar a falha
  que deveria explicar.

### Interno

- A imagem agora **sobe** antes de ser publicada, contra um PostgreSQL de
  verdade: as migrations têm de aplicar, a página tem de servir o runtime do
  Blazor e as colunas têm de ter a largura declarada. Se qualquer uma falhar,
  nada vai para o Docker Hub. Os três últimos bugs passaram no build e nos
  testes e só apareceram depois do deploy.
- A suíte passou a exercer os limites de coluna num PostgreSQL de verdade. O
  SQLite aceita qualquer texto num `varchar(n)` e ignora o limite declarado — é
  por isso que essas falhas chegavam intactas em produção.

### Atualizar

```bash
docker compose pull && docker compose up -d --force-recreate
```

O `--force-recreate` não é enfeite: sem ele o `pull` baixa a imagem nova e o
container **continua rodando a antiga**, o que faz o problema parecer não
corrigido. A migração das colunas roda sozinha no arranque e não perde dados.

## [0.1.1] — 2026-08-22

### Corrigido

- **Importar packs do CurseForge falhava em instalações com PostgreSQL**, com
  `value too long for type character varying(512)`, deixando o modpack criado
  sem nenhuma versão. A causa era aritmética: o identificador interno de um
  override é o caminho do arquivo **mais um prefixo**, e os dois campos tinham o
  mesmo limite — então um caminho no tamanho máximo gerava um identificador que
  não cabia por definição. O limite do identificador passou a ser **derivado** do
  limite do caminho, o que impede os dois de divergirem de novo.
- Caminhos aceitam até 1024 caracteres, tamanho que packs grandes realmente
  alcançam.
- URLs de ícone foram alargadas: um link de CDN com assinatura passa de 512 com
  facilidade, e isso quebraria na etapa seguinte, ao baixar os mods.
- Um arquivo que ainda assim exceda o limite é **ignorado** em vez de derrubar a
  importação inteira; a contagem aparece no acompanhamento.

Instalações com SQLite não eram afetadas — o SQLite não aplica limites de
tamanho em texto. A migração das colunas roda sozinha no arranque e não perde
dados.

> Se você tinha um modpack criado por uma importação que falhou, ele ficou sem
> versão nenhuma: apague e importe de novo, ou importe por cima para criar a
> versão.

## [0.1.0] — 2026-08-22

Primeira versão publicada do TCMine Server.

> **Por que 0.1.0 e não 1.0.0.** O conjunto de funcionalidades está inteiro e
> testado, mas instalar em ambientes variados continua revelando arestas. Um
> `1.0` promete estabilidade para depender; `0.x` descreve o que isto é hoje.

### Adicionado

- **Modpacks** — catálogo com versões imutáveis; ingestão de mods do Modrinth e
  do CurseForge com busca unificada; importação de packs inteiros das duas
  origens; atualização vinda da origem por merge de três vias, preservando o que
  você customizou; editor de overrides no navegador; upload manual de arquivos.
- **Servidores de jogo** — cada servidor roda como container
  `itzg/minecraft-server` com a versão do modpack fixada no próprio servidor;
  materialização da instância por hardlink dos jars, sem tocar no mundo do
  jogador; console ao vivo e comandos por RCON; métricas de CPU, memória e
  jogadores.
- **Backups de mundo** — snapshot manual ou automático antes de cada troca de
  versão, com a troca cancelada se o backup falhar; backup a quente, sem
  desconectar ninguém.
- **Acesso** — login local para o administrador e login pelo perfil Minecraft
  verificado para os jogadores; convites de uso único por servidor e por papel;
  perda de papel corta o acesso na hora.
- **E-mail** — SMTP configurável pelo painel, com senha cifrada e botão de
  teste; alternativa com servidor de e-mail próprio como container.

[0.3.0]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.3.0
[0.2.0]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.2.0
[0.1.7]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.7
[0.1.6]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.6
[0.1.5]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.5
[0.1.4]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.4
[0.1.3]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.3
[0.1.2]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.2
[0.1.1]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.1
[0.1.0]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.0
