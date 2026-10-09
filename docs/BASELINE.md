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

| #  | Medição                                   | 1        | 2        | 3        | Mediana                                         | Meta |
|----|-------------------------------------------|----------|----------|----------|-------------------------------------------------|------|
| S1 | Boot até `/health/live` responder         |          |          |          | _falta medir_                                   |      |
| S2 | `ModpackDetailPage` (carga / render)      | 79 / 42  | 86 / 44  | 23 / 10  | **79 / 42 ms**                                  |      |
| S3 | `ModpackOverridesPage`                    | 185 / 61 | 520 / 40 | 182 / 35 | **185 / 40 ms**                                 |      |
| S4 | `CloudVaultPage`                          | 67 / 37  | 14 / 16  | 6 / 23   | **14 / 23 ms**                                  |      |
| S5 | Iniciar um servidor de jogo até `Running` |          |          |          | _falta medir_                                   |      |
| S6 | Memória do container em repouso           | 227 MiB  |          |          | 227 MiB (1 amostra, servidores de jogo parados) |      |

### Como medir

```bash
./scripts/baseline.sh boot  tcmine-tcmine-1          # S1 — derruba o painel 3 vezes
./scripts/baseline.sh pages tcmine-tcmine-1          # S2–S4 — depois de abrir as páginas pelo menu
./scripts/baseline.sh mem   tcmine-tcmine-1          # S6
```

As páginas são abertas navegando pelo menu do painel (não por F5). Cada linha
"Abertura de página" traz **carga** (consultas e montagem dos dados) e **renderização** (do fim da carga até o lote de
renderização concluir). O
cronômetro (`PageLoadTimer`) é temporário e sai no fim da fase 8.

### O que os números dizem

- A renderização é pequena e constante (10–61 ms): na ABERTURA, o tamanho dos
  componentes não pesa. O cronômetro não mede o uso (digitar, trocar de aba).
- A primeira página de uma sessão parada custa mais (o 520 ms de S3 veio depois
  de ~14 min sem uso); reabertas, S2 e S4 caem para 23 e 6–14 ms.
- S3 é a única que não esquenta: fica em ~180 ms. Ela carrega o modpack com
  todas as versões e arquivos para filtrar só os overrides.

Limite conhecido: as abas da `CloudVaultPage` carregam os próprios dados ao
abrir e ficam de fora — a auditoria da nuvem é medida à parte na fase 5.

## Launcher

Os três marcos saem do log `%LOCALAPPDATA%\TCMine\logs\launcher-AAAAMMDD.log`,
linhas "Arranque:", contados desde o início do processo:

```powershell
Select-String -Path "$env:LOCALAPPDATA\TCMine\logs\launcher-*.log" -Pattern "Arranque:|Canal aberto" |
  Select-Object -Last 12 | ForEach-Object { $_.Line }
```

| #  | Medição                                                    | 1    | 2    | 3   | Mediana                                   | Meta |
|----|------------------------------------------------------------|------|------|-----|-------------------------------------------|------|
| L1 | Processo → host montado (a quente)                         | 282  | 280  | 278 | **280 ms**                                |      |
| L2 | Processo → janela visível (a quente)                       | 915  | 923  | 907 | **915 ms**                                |      |
| L3 | Processo → primeira tela utilizável (a quente, com sessão) | 3563 | 3245 |     | ~3,4 s (2 amostras)                       |      |
| L4 | Idem, primeira abertura após atualizar                     | 9932 |      |     | 9,9 s (1 amostra; host 2869, janela 4871) |      |
| L5 | Instalar o modpack de referência do zero                   |      |      |     | ≥ 37 s só de download (ver abaixo)        |      |
| L6 | Atualizar para a versão seguinte                           |      |      |     | _falta medir_                             |      |

### O que os números dizem

- A quente, o launcher mostra a janela em menos de 1 s e os valores quase não
  variam. O arranque do processo não é o problema.
- Da janela à tela utilizável vão 2,3–2,6 s, dos quais 1,2–1,5 s são cinco
  chamadas de rede EM SÉRIE (handshake, Xbox, XSTS, login do Minecraft, sessão
  no TCMine). É o alvo da fase 8.
- A frio o custo está antes da janela (host em 2,9 s contra 0,28 s): carga do
  runtime e dos binários do disco.

### Instalação do zero (L5)

Uma instalação com o estado local limpo, em 2026-10-09: **3 569 arquivos, um
pedido HTTP por arquivo, todos 200**, do primeiro ao último pedido em **37,4 s**.
O ritmo estabiliza em ~150 pedidos/s com 6 downloads simultâneos (`InstallModpackVersion.ParallelDownloads`) e ~37 ms
até os cabeçalhos de cada
resposta.

O log não marca o clique nem o fim da aplicação dos arquivos, então 37 s é o
piso, não o total — o total é por cronômetro, do clique até "Jogar".

Conta que orienta a fase 8: 3 569 pedidos × 37 ms ÷ 6 em paralelo ≈ 22 s. Mais
da metade do tempo de download é ida-e-volta por arquivo, não largura de banda.

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

| #  | Hoje                     | Meta                        | Fase |
|----|--------------------------|-----------------------------|------|
| S2 | 79 / 42 ms               | não piorar                  | —    |
| S3 | 185 / 40 ms              | carga ≤ 60 ms               | 6    |
| S4 | 14 / 23 ms               | não piorar                  | —    |
| S6 | 227 MiB                  | não piorar (≤ 250 MiB)      | —    |
| L2 | 915 ms                   | não piorar                  | —    |
| L3 | ~3,4 s                   | ≤ 1,5 s                     | 8    |
| L5 | ≥ 37 s                   | _definir após o cronômetro_ | 8    |
| —  | 14 chaves / 2 servidores | 1 chave por servidor        | 5    |

S1, S5 e L6 recebem meta quando forem medidos.
