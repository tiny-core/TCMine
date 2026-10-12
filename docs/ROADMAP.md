# Roadmap

O que está combinado e ainda não foi feito, em ordem de prioridade. Cada item
diz o porquê e onde começar, para quem pegar a tarefa (pessoa ou agente) não
precisar reconstruir o contexto. Ao concluir um item, apague-o daqui e registre
no CHANGELOG.

## 1. Melhorias apontadas e não feitas

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
