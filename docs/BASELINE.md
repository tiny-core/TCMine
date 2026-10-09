# Linha de base de performance

Números de partida da refatoração, medidos na versão **1.1.1** — a primeira com a
instrumentação, e sem nenhuma mudança de comportamento em relação à 1.1.0. As
fases 6 e 8 só fecham quando a meta de cada linha é atingida ou a diferença é
explicada.

## Ambiente da medição

Sem isto os números não são comparáveis depois. Repetir as medições futuras na
mesma máquina e com o mesmo modpack.

| Item                  | Valor                                                             |
|-----------------------|-------------------------------------------------------------------|
| Versão                | servidor 1.1.1, launcher 1.1.1 (`server-v1.1.1`, `933a059`)       |
| Data                  | 2026-10-09                                                        |
| Servidor              | Ubuntu Server, Docker, 14 GiB de RAM visíveis ao container        |
| Provider do banco     | Postgres                                                          |
| Launcher              | instalado (Release), Windows — CPU/RAM/disco: _preencher_         |
| Modpack de referência | All the Mods 10 — 6 versões, 22 185 arquivos (≈ 3 570 por versão) |

## Regras

- Build de Release, na imagem publicada — nunca `dotnet run` nem Debug.
- Cada medição 3 vezes; vale a mediana.
- "A frio" = primeira vez depois de reiniciar a máquina. "A quente" = segunda vez em diante.
- Mesmo modpack de referência em todas as medições que envolvem modpack.

## Servidor

Tudo é medido do HOST, pelo nome do container — nada roda dentro dele.

| #  | Medição                                   | Amostras (carga / renderização)   | Mediana                                         |
|----|-------------------------------------------|-----------------------------------|-------------------------------------------------|
| S1 | Boot até `/health/live` responder         |                                   | _falta medir_                                   |
| S2 | `ModpackDetailPage`                       | 58/11, 17/19, 16/18, 17/16, 13/21 | **17 / 18 ms**                                  |
| S3 | `ModpackOverridesPage`                    | 160/48, 203/37, 196/36, 163/35    | **179 / 36 ms**                                 |
| S4 | `CloudVaultPage`                          | 87/41, 16/16, 39/36, 7/23         | **27 / 29 ms**                                  |
| S5 | Iniciar um servidor de jogo até `Running` | 684 (container 538)               | 684 ms (1 amostra, container já criado)         |
| S6 | Memória do container em repouso           | 227 MiB                           | 227 MiB (1 amostra, servidores de jogo parados) |

S2–S5 são a saída do `report` na 1.1.2. Uma rodada anterior, na 1.1.1, deu
79/42, 185/40 e 14/23 ms para S2, S3 e S4 — a mesma ordem de grandeza.

### Como medir

```bash
./scripts/baseline.sh boot  tcmine-tcmine-1          # S1 — derruba o painel 3 vezes
./scripts/baseline.sh report tcmine-tcmine-1         # S2–S5 — amostras e medianas, lidas do log
./scripts/baseline.sh mem   tcmine-tcmine-1          # S6
```

O `report` não mede nada sozinho: ele lê do log o que o uso normal deixou.
Abra as páginas pelo menu, inicie um servidor de jogo, e rode o comando no fim.

**S5 mede o que o TCMine controla** — materializar a pasta, criar e iniciar o
container (linha "Início de servidor"). `Running` é "container no ar", e não
"Minecraft aceitando jogadores": a carga da JVM e dos mods de um ATM10 leva
minutos depois disso e está fora do alcance desta refatoração.

As páginas são abertas navegando pelo menu do painel (não por F5). Cada linha
"Abertura de página" traz **carga** (consultas e montagem dos dados) e **renderização** (do fim da carga até o lote de
renderização concluir). O
cronômetro (`PageLoadTimer`) é temporário e sai no fim da fase 8.

### O que os números dizem

- A renderização é pequena e constante (10–61 ms): na ABERTURA, o tamanho dos
  componentes não pesa. O cronômetro não mede o uso (digitar, trocar de aba).
- A primeira página de uma sessão parada custa mais (58 e 87 ms na primeira
  abertura de S2 e S4; na rodada da 1.1.1, um 520 ms em S3 depois de ~14 min sem
  uso); reabertas, S2 e S4 ficam em 13–17 e 7–39 ms.
- S3 é a única que não esquenta: 160–203 ms em todas as aberturas. Ela carrega o modpack com
  todas as versões e arquivos para filtrar só os overrides.

Limite conhecido: as abas da `CloudVaultPage` carregam os próprios dados ao
abrir e ficam de fora — a auditoria da nuvem é medida à parte na fase 5.

## Launcher

Os três marcos saem do log `%LOCALAPPDATA%\TCMine\logs\launcher-AAAAMMDD.log`,
linhas "Arranque:", contados desde o início do processo:

```powershell
.\scripts\baseline.ps1 report          # log de hoje; -Days 3 para os últimos 3 dias
```

O comando separa a "primeira tela utilizável" pelo desfecho do arranque (com
sessão, login, offline…), porque são caminhos de custo diferente, e lista as
linhas "Instalação de modpack" — uma por instalação ou atualização, com o tempo
de cada fase (plano, download, aplicação, fecho).

| #  | Medição                                                    | 1     | 2     | 3   | Mediana                                                | Meta |
|----|------------------------------------------------------------|-------|-------|-----|--------------------------------------------------------|------|
| L1 | Processo → host montado (a quente)                         | 282   | 280   | 278 | **280 ms**                                             |      |
| L2 | Processo → janela visível (a quente)                       | 915   | 923   | 907 | **915 ms**                                             |      |
| L3 | Processo → primeira tela utilizável (a quente, com sessão) | 3563  | 3245  |     | ~3,4 s (2 amostras)                                    |      |
| L4 | Idem, primeira abertura após atualizar                     | 9932  | 11168 |     | ~10,5 s (2 amostras; host 2,9–5,1 s, janela 4,9–7,0 s) |      |
| L5 | Instalar o modpack de referência do zero                   | 49186 |       |     | **49,2 s** (1 amostra; fases abaixo)                   |      |
| L6 | Atualizar para a versão seguinte                           |       |       |     | _falta medir_                                          |      |

### O que os números dizem

- A quente, o launcher mostra a janela em menos de 1 s e os valores quase não
  variam. O arranque do processo não é o problema.
- Da janela à tela utilizável vão 2,3–2,6 s, dos quais 1,2–1,5 s são cinco
  chamadas de rede EM SÉRIE (handshake, Xbox, XSTS, login do Minecraft, sessão
  no TCMine). É o alvo da fase 8.
- A frio o custo está antes da janela (host em 2,9 s contra 0,28 s): carga do
  runtime e dos binários do disco.

### Instalação do zero (L5)

All the Mods 10 1.2.1, com o estado local limpo, na 1.1.2 (2026-10-09):

| Fase      | Tempo      | O que faz                                            |
|-----------|------------|------------------------------------------------------|
| Plano     | 84 ms      | manifesto do servidor e diff contra o disco          |
| Download  | **45,5 s** | 3 569 arquivos, 1,58 GB — um pedido HTTP por arquivo |
| Aplicação | 3,6 s      | 3 692 arquivos ligados do store para a instância     |
| Fecho     | 26 ms      | limpeza e gravação do manifesto                      |
| **Total** | **49,2 s** |                                                      |

O download é 92% do total. Ele tem duas metades de natureza diferente, visíveis
no log HTTP de uma instalação anterior (37 s do primeiro ao último pedido):

- **Os jars grandes saem primeiro** (a fila é ordenada por tamanho): poucos
  pedidos por segundo, limitados pela largura de banda. É onde vai quase todo o
  1,58 GB — a média da instalação inteira é ~35 MB/s.
- **Depois vêm milhares de arquivos pequenos** (configs, overrides): o ritmo
  estabiliza em ~150 pedidos/s com 6 downloads simultâneos (`InstallModpackVersion.ParallelDownloads`) e ~37 ms até os
  cabeçalhos de
  cada resposta. Aí o limite é a ida-e-volta, não a banda: ~3 400 pedidos ×
  37 ms ÷ 6 ≈ 21 s para transferir poucos megabytes.

É o alvo da fase 8 no launcher: reduzir o número de idas-e-voltas dos arquivos
pequenos (agrupá-los num pedido, ou mais paralelismo só para eles). O plano e a
aplicação não justificam trabalho.

## Banco

Postgres, 2026-10-09. Soma das tabelas ≈ 18 MB; pasta de blobs: 1,6 GB.

```bash
./scripts/baseline.sh db-postgres <container-do-postgres> [usuário] [banco]
```

| Tabela                     | Linhas | Tamanho |
|----------------------------|-------:|--------:|
| `modpack_files`            | 22 211 |   16 MB |
| `cloud_ledger`             |     15 |   80 kB |
| `cloud_server_credentials` |     14 |   72 kB |
| `cloud_item_types`         |     13 |   48 kB |
| `cloud_balances`           |     13 |   56 kB |
| `modpack_versions`         |      8 |  632 kB |
| `cloud_batches`            |      5 |   56 kB |
| `activity_events`          |      5 |   32 kB |
| `cloud_admin_audit`        |      3 |   48 kB |
| `game_servers`             |      2 |   96 kB |
| `modpacks`                 |      2 |   80 kB |
| `blobs`                    |      0 |   16 kB |

As demais tabelas têm 0–2 linhas.

- `cloud_server_credentials`: **14 chaves para 2 servidores de jogo** — o
  acúmulo que a fase 5.1 resolve, já visível com pouco uso.
- `blobs`: 0 linhas — confirma que a tabela não é usada (fase 2).
- O banco é pequeno hoje. As metas da fase 5 são sobre TETO de crescimento, e
  não sobre o tamanho atual.

## Metas

Uma por medição que a refatoração pretende mexer; as outras só não podem piorar.

| #  | Hoje                     | Meta                   | Fase |
|----|--------------------------|------------------------|------|
| S2 | 17 / 18 ms               | não piorar             | —    |
| S3 | 179 / 36 ms              | carga ≤ 60 ms          | 6    |
| S4 | 27 / 29 ms               | não piorar             | —    |
| S6 | 227 MiB                  | não piorar (≤ 250 MiB) | —    |
| L2 | 915 ms                   | não piorar             | —    |
| L3 | ~3,4 s                   | ≤ 1,5 s                | 8    |
| L5 | 49,2 s (download 45,5 s) | ≤ 30 s                 | 8    |
| —  | 14 chaves / 2 servidores | 1 chave por servidor   | 5    |

S5 (684 ms com o container já criado) só não pode piorar. S1 e L6 recebem meta
quando forem medidos; falta também S5 no PRIMEIRO start de um servidor, que
inclui materializar a pasta.
