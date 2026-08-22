# Changelog

Todas as mudanças relevantes do **TCMine Server**. As versões seguem
[SemVer](https://semver.org/lang/pt-BR/); enquanto estivermos em `0.x`, a API e
o formato dos dados ainda podem mudar entre versões menores.

O texto completo de cada lançamento está na
[página de releases](https://github.com/tiny-core/TCMine/releases).

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

[0.1.1]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.1
[0.1.0]: https://github.com/tiny-core/TCMine/releases/tag/server-v0.1.0
