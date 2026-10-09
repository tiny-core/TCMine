# Linha de base de performance

Números de partida da refatoração, medidos no ramo `refactor` antes de qualquer
mudança de comportamento (o código de produção é o do commit `5809154`). As fases 6 e 8
só fecham quando a meta de cada linha é atingida ou a diferença é explicada.

## Ambiente da medição

Sem isto os números não são comparáveis depois. Preencher uma vez e repetir as
medições futuras na mesma máquina.

| Item                                              | Valor                                  |
|---------------------------------------------------|----------------------------------------|
| Commit                                            | ramo `refactor`, a partir de `5809154` |
| Data                                              |                                        |
| Máquina do servidor (CPU, RAM, disco SSD/HDD)     |                                        |
| Imagem do servidor (tag)                          |                                        |
| Provider do banco                                 | Sqlite / Postgres                      |
| Máquina do launcher (CPU, RAM, disco)             |                                        |
| Versão do launcher                                |                                        |
| Modpack de referência (nome, nº de mods, tamanho) |                                        |

## Regras

- Build de Release, na imagem publicada — nunca `dotnet run` nem Debug.
- Cada medição 3 vezes; vale a mediana.
- "A frio" = primeira vez depois de reiniciar o processo. "A quente" = segunda vez em diante.
- Mesmo modpack de referência em todas as medições que envolvem modpack.

## Servidor

| #  | Medição                                               | Como medir                                                                 | 1 | 2 | 3 | Mediana | Meta |
|----|-------------------------------------------------------|----------------------------------------------------------------------------|---|---|---|---------|------|
| S1 | Boot até `/health/live` responder                     | `scripts/baseline.sh boot`                                                 |   |   |   |         |      |
| S2 | Abrir `ModpackDetailPage` (a frio / a quente)         | ver "Páginas do painel"                                                    |   |   |   |         |      |
| S3 | Abrir `ModpackOverridesPage` (a frio / a quente)      | idem                                                                       |   |   |   |         |      |
| S4 | Abrir `CloudVaultPage` (a frio / a quente)            | idem                                                                       |   |   |   |         |      |
| S5 | Materializar instância + start, do clique a `Running` | cronômetro do clique até o status mudar; conferir com os timestamps do log |   |   |   |         |      |
| S6 | Memória do container em repouso                       | `scripts/baseline.sh mem`                                                  |   |   |   |         |      |

### Páginas do painel

Navegar para a página pelo menu do painel (não por F5) e ler a linha
"Abertura de página" do log:

```bash
docker compose logs --since 5m tcmine | grep "Abertura de página"
```

Cada linha traz dois números: **carga** (consultas ao banco e montagem dos
dados) e **renderização** (do fim da carga até o lote de renderização ser
concluído). Anotar os dois, como `carga / renderização`. O cronômetro (`PageLoadTimer`) é temporário e sai no fim da
fase 8.

Limite conhecido: mede só a página. As abas da `CloudVaultPage` carregam os
próprios dados ao abrir e ficam de fora — a auditoria da nuvem é medida à parte
na fase 6.

## Launcher

Os três marcos saem do log `%LOCALAPPDATA%\TCMine\logs\launcher-AAAAMMDD.log`,
linhas "Arranque:". "A frio" aqui = primeira abertura depois de reiniciar o Windows.

| #  | Medição                                                  | 1 | 2 | 3 | Mediana | Meta |
|----|----------------------------------------------------------|---|---|---|---------|------|
| L1 | Processo → host montado                                  |   |   |   |         |      |
| L2 | Processo → janela visível                                |   |   |   |         |      |
| L3 | Processo → primeira tela utilizável (a frio)             |   |   |   |         |      |
| L4 | Processo → primeira tela utilizável (a quente)           |   |   |   |         |      |
| L5 | Instalar o modpack de referência do zero                 |   |   |   |         |      |
| L6 | Atualizar o modpack de referência para a versão seguinte |   |   |   |         |      |

L5 e L6: cronômetro do clique até o botão "Jogar" ficar disponível. Anotar também
a velocidade da ligação, porque o download domina.

## Banco

Saída de `scripts/baseline.sh db-sqlite <caminho>` ou da consulta Postgres do mesmo script.

| Item                      | Valor |
|---------------------------|-------|
| Tamanho total do banco    |       |
| Tamanho da pasta de blobs |       |

| Tabela | Linhas | Tamanho (só Postgres) |
|--------|--------|-----------------------|
|        |        |                       |

## Metas

Preenchidas na tarefa 1.3, depois de os números existirem. Cada meta diz o valor
alvo e a fase que deve atingi-lo.
