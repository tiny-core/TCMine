# Changelog

Todas as mudanças relevantes do **TCMine Server**. As versões seguem
[SemVer](https://semver.org/lang/pt-BR/); enquanto estivermos em `0.x`, a API e
o formato dos dados ainda podem mudar entre versões menores.

O texto completo de cada lançamento está na
[página de releases](https://github.com/tiny-core/TCMine/releases).

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

[0.1.5]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.5
[0.1.4]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.4
[0.1.3]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.3
[0.1.2]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.2
[0.1.1]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.1
[0.1.0]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.0
