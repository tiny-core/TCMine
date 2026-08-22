# Changelog

Todas as mudanças relevantes do **TCMine Server**. As versões seguem
[SemVer](https://semver.org/lang/pt-BR/); enquanto estivermos em `0.x`, a API e
o formato dos dados ainda podem mudar entre versões menores.

O texto completo de cada lançamento está na
[página de releases](https://github.com/tiny-core/TCMine/releases).

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

[0.1.2]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.2
[0.1.1]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.1
[0.1.0]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.0
