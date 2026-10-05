# Roadmap

O que está combinado e ainda não foi feito, em ordem de prioridade. Cada item
diz o porquê e onde começar, para quem pegar a tarefa (pessoa ou agente) não
precisar reconstruir o contexto. Ao concluir um item, apague-o daqui e registre
no CHANGELOG.

## 1. Lançar a 1.1.0

- Criar a tag `server-v1.1.0` na `master` (Releases do GitHub ou
  `git tag server-v1.1.0 && git push origin server-v1.1.0`). A imagem leva o
  launcher 1.1.0 (downloads paralelos, Entrar no servidor, trava com o jogo
  aberto), o console com RCON e o visual novo.
- No servidor: `docker compose pull && docker compose up -d`. Se houver CDN ou
  proxy na frente, limpar o cache de `/updates/launcher/*` uma vez.

## 2. Terminar a reformulação visual

Referências: o protótipo aprovado (artifact "TCMine — proposta de redesenho")
e o **TCMine Design System** (paleta pedra vulcânica, regras de texto e de
interação). Tema, menu, visão geral e página pública já estão no código.

- **Demais telas do painel**: armazenamento, configurações, diálogos.
  Aplicar a mesma linguagem (cartões com borda fina, rótulos micro, chips
  em minúsculas, estado sempre visível). Uma tela por PR. (Servidores,
  inventário de mods, nuvem e usuários já feitas. A nuvem só cobriu a
  lista — `CloudVaultsPage` — e os chips capitalizados que apareceram nos
  painéis; os demais painéis de uma nuvem específica — servidores,
  jogadores, regras, quarentena, em dúvida, incidentes, auditoria,
  configurações — não foram revisados tela a tela.)
- **Console**: levar o console para a linguagem nova (fundo afundado, eco do
  comando, referência de comandos num painel lateral).

## 3. Dados que o painel ainda não registra

O protótipo mostra três coisas que hoje não existem; a visão geral as deixou
de fora para não exibir números inventados.

- **Atividade recente** (quem entrou, versão publicada, servidor caiu, backup):
  precisa de um registro de eventos no Domain/Application e de quem o
  alimente (orquestrador, publicação, contagem de jogadores).
- **Pico de jogadores do dia**: o `PlayerCountCache` só guarda o valor atual.
- **Número de mods e tamanho por modpack na página pública**: o
  `PublicModpackView` não carrega; `GetVersionStatsAsync` já agrega por versão.

## 4. Melhorias apontadas e não feitas

- **Fonte Inter**: o CSP do painel (`font-src 'self'`) impede o Google Fonts, e
  os binários não estão no repositório. Para usá-la, colocar os `.woff2` no
  `TCMine.UI.Shared/wwwroot` com `@font-face` — vale para o launcher, que não
  pode depender da rede.
- **Overrides**: a página ainda alterna o tema do Monaco por `IsDarkMode`, que
  agora é sempre verdadeiro; dá para simplificar.
- **Launcher sem assinatura**: o SmartScreen avisa ao instalar. Assinar o
  pacote exige certificado de code signing.
- **Tamanho da imagem** (~1 GB por levar o launcher e o `vpk`): avaliar
  descartar o `vpk` depois de empacotar, ou empacotar no build da imagem com
  um endereço genérico.
