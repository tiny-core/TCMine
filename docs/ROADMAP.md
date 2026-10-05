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

## 2. Melhorias apontadas e não feitas

- **Launcher sem assinatura**: o SmartScreen avisa ao instalar. Assinar o
  pacote exige certificado de code signing — depende de comprar um
  certificado, fora do alcance de quem só edita código.
- **Tamanho da imagem** (~1 GB por levar o launcher e o `vpk`). Avaliado, não
  implementado — risco e ganho não medidos pedem uma decisão, não só código:
  - Hoje o `vpk` (ferramenta inteira, só podada ao build do runtime — ver
    comentário no `Dockerfile`) viaja na imagem FINAL porque
    `LauncherBundlePublisher`/`VelopackLauncherBundle` empacotam o launcher no
    ARRANQUE do container, quando o endereço público da instalação já é
    conhecido.
  - Empacotar no BUILD da imagem (com um endereço genérico/placeholder no
    `server.json`, substituído por uma edição de arquivo no arranque, não por
    `vpk pack` de novo) tiraria a ferramenta `vpk` inteira do estágio final —
    ela ficaria só no estágio de build, descartado pelo multi-stage. É o
    ganho real.
  - O risco: `LauncherBundlePlan.Decide` (com teste) hoje decide reempacotar
    quando o endereço muda — exatamente o caso que a build única não cobre.
    Precisaria de uma segunda via (reempacotar em runtime SE o endereço virar
    diferente do placeholder) ao lado da nova, não no lugar dela, e isso é
    medir com uma imagem real — não dá para validar sem subir o container.
