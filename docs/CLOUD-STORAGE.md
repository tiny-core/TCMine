# Nuvem de itens (TC Cloud Storage) — plano

> Estado: **proposta**, nada implementado. Consumidor: mod `tccloud` (NeoForge 1.21.1), plano em
> `docs/planos/tc-cloud-storage.md` do workspace **tc-minecraft-mods**. As regras que não podem ser
> esquecidas estão resumidas na §13 do `CLAUDE.md`.

## 1. Por que existe

O jogador guarda itens numa nuvem do **dono** do servidor e os reencontra em outro servidor do mesmo
dono (outro modpack, outro mundo). O mod do jogo expõe a nuvem para a rede do AE2; o TCMine é a
fonte da verdade dos saldos, das regras e da auditoria. O mod só confirma uma mudança depois que o
mundo que a causou foi salvo, e só um servidor por vez segura o canal de um jogador (lease com
época). O que sai da nuvem é gravado cedo e o que entra só depois que o mundo sem o item está em
disco ("débito cedo, crédito tarde"). Isso torna crash, rollback e restauração de backup tratáveis sem duplicar itens.

## 2. Domínio (`TCMine.Server.Domain/Cloud/`)

| Entidade                      | Campos principais                                                                                                                                                                                                               | Regras                                                                            |
|-------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| `CloudVault` : `IOwnedEntity` | `Name`, `OwnerId`, `PolicyMode` (`Blocklist`/`Allowlist`), `PolicyVersion` (long), `LeaseTtlMinutes` (30), `MaxEncodedBytes` (8192), `MaxChannelsPerPlayer` (5), `MaxTypesPerChannel`, `MaxTotalPerChannel` (long), `IsEnabled` | mudar uma regra incrementa `PolicyVersion`                                        |
| `CloudServerCredential`       | `GameServerId` (único), `VaultId`, `KeyPrefix` (8 chars visíveis), `KeyHash` (SHA-256), `CreatedAt`, `RevokedAt?`, `LastSeenAt?`, `ModVersion?`, `WorldId?`                                                                     | uma ativa por servidor; o segredo só existe em memória ao gerar                   |
| `CloudChannel`                | `VaultId`, `PlayerUuid` (sem hífens, igual a `User.MinecraftUuid`), `Name`, `Status` (`Active`/`Frozen`), `FrozenReason?`                                                                                                       | nome único por (vault, jogador); apagar só se vazio                               |
| `CloudItemType`               | `Fingerprint` (SHA-256 hex, único global), `ItemId` (`mod:item`), `ModId`, `EncodedItem` (bytes), `DisplayName`, `FirstSeenAt`                                                                                                  | imutável; deduplicado entre nuvens (conteúdo endereçado, como o blob store)       |
| `CloudBalance`                | PK (`ChannelId`, `ItemTypeId`), `Amount` (long ≥ 0)                                                                                                                                                                             | linha removida quando chega a 0                                                   |
| `CloudLease`                  | PK (`VaultId`, `PlayerUuid`), `HolderServerId?`, `Epoch` (long, nunca diminui), `LastSeq`, `State` (`Free`/`Held`/`Releasing`), `HeartbeatAt`, `ExpiresAt`, `Version` (token de concorrência)                                   | linha nunca apagada: a época precisa sobreviver                                   |
| `CloudBatch`                  | `VaultId`, `PlayerUuid`, `ServerId`, `Epoch`, `Seq`, `Status` (`Applied`/`Quarantined`/`Discarded`/`Reverted`), `PayloadHash`, `ReceivedAt`                                                                                     | único (VaultId, PlayerUuid, Epoch, Seq)                                           |
| `CloudLedgerEntry`            | `ChannelId`, `ItemTypeId`, `Delta`, `BalanceAfter`, `Source` (`Game`/`Admin`/`Revert`/`QuarantineApply`), `BatchId?`, `ActorUserId?`, `Reason?`, `CreatedAt`                                                                    | append-only: o código não tem método de Update/Delete                             |
| `CloudQuarantine`             | `BatchId`, `Reason` (`StaleEpoch`/`NegativeBalance`/`Divergence`/`QuotaExceeded`), `Payload` (JSON), `ResolvedAt?`, `ResolvedBy?`, `Resolution?`                                                                                | aplicar só se o lease estiver livre                                               |
| `CloudItemRule`               | `VaultId`, `Scope` (`Item`/`Mod`/`Tag`), `Pattern`, `Action` (`Allow`/`Block`), `Note`, `CreatedBy`                                                                                                                             | —                                                                                 |
| `CloudSuspectItem`            | `VaultId`, `ItemId`, `Reason`, `Count`, `FirstSeenAt`, `LastSeenAt`, `Status` (`Pending`/`Allowed`/`Blocked`)                                                                                                                   | decidir cria um `CloudItemRule`                                                   |
| `CloudRollbackIncident`       | `VaultId`, `ServerId`, `WorldId`, `Checkpoint` (JSON), `DetectedAt`, `Origin` (`Restore`/`Hello`), `Status` (`Open`/`Reverted`/`Accepted`), `ResolvedBy?`                                                                       | aberto = servidor em somente leitura na nuvem                                     |
| `CloudDoubtfulOperation`      | `VaultId`, `ServerId`, `PlayerUuid`, `ChannelId`, `ItemTypeId`, `Kind` (`PendingCredit`/`RecentDebit`), `Amount`, `OccurredAt`, `Status` (`Open`/`Refunded`/`Dismissed`), `ResolvedBy?`                                         | reportadas pelo mod no boot após crash; devolver = linha no ledger (Source=Admin) |
| `CloudAdminAuditEntry`        | `ActorUserId`, `Action`, `TargetType`, `TargetId`, `Details` (JSON), `CreatedAt`                                                                                                                                                | toda ação do painel                                                               |

Mudanças em entidades existentes:

- `GameServer.CloudVaultId?` (nulo = nuvem desligada). A regra de domínio
  `AttachToVault(vault)` exige `vault.OwnerId == OwnerId`.
- (Plano original, substituído na fatia E — ver §10) O orquestrador (`EnsureCreatedAsync`) injeta `TCMINE_CLOUD_URL`/
  `TCMINE_CLOUD_KEY` quando há
  credencial ativa e força `ONLINE_MODE=true`.

Tabelas: `cloud_vaults`, `cloud_server_credentials`, `cloud_channels`, `cloud_item_types`,
`cloud_balances`, `cloud_leases`, `cloud_batches`, `cloud_ledger`, `cloud_quarantine`,
`cloud_item_rules`, `cloud_suspect_items`, `cloud_rollback_incidents`, `cloud_doubtful_operations`,
`cloud_admin_audit`.
Índices: `cloud_ledger(ChannelId, Id)`, `cloud_batches(ServerId, Epoch, Seq)`,
`cloud_balances(ChannelId)`, `cloud_channels(VaultId, PlayerUuid)`.
`Amount`/`Delta` como `bigint` e testados no `PostgresColumnLimitsTests`.

## 3. Casos de uso (`TCMine.Server.Application/Cloud/`)

**API do mod** (autenticada pela chave do servidor):

- `CloudHello`: registra versão/`WorldId`; compara o checkpoint com o último lote aplicado do
  servidor e abre incidente se o mundo voltou no tempo; devolve config, `PolicyVersion` e
  incidentes abertos.
- `AcquireLease`: livre ou expirado → `Epoch++`, `Held` por este servidor, snapshot dos canais;
  ocupado → `Result.Fail` com servidor e desde quando; incidente aberto ou canal congelado →
  snapshot em modo somente leitura.
- `Heartbeat` (lote de leases): renova `ExpiresAt`; devolve comandos (`Freeze`, `ForceRelease`,
  `PolicyChanged`).
- `ApplyBatch`: transação → valida credencial, lease (holder + época), seq (= `LastSeq + 1`;
  ≤ `LastSeq` com o mesmo `PayloadHash` → "duplicado", sucesso), cotas, saldos ≥ 0, saldos
  esperados → grava ledger + saldos + `LastSeq` + `Version++`. Qualquer falha de regra → quarentena
    + congela o canal + `Result` com o motivo.
- `ReleaseLease`: só se `LastSeq` bate (todos os lotes chegaram); senão, `Releasing` até chegarem.
- `CreateChannel` / `RenameChannel` / `DeleteEmptyChannel`: exigem lease do jogador neste servidor.
- `ReportSuspects`: soma em `CloudSuspectItem`.
- `ReportDoubtful`: grava `CloudDoubtfulOperation` (idempotente por servidor + época + seq).

**Painel:**

- Nuvens: `CreateVault`, `UpdateVaultSettings`, `AttachServer`/`DetachServer`,
  `IssueServerKey`/`RevokeServerKey` (a nova chave aparece uma vez, ou vai direto para o container).
- Jogadores: `SearchChannels` (por nome/UUID), `GetChannelBalances`, `GetLedger` (paginado por
  `Id`), `FreezeChannel`/`UnfreezeChannel`, `AdjustBalance` (exige lease livre e motivo).
- Leases: `ListLeases`, `ForceReleaseLease` (avisa: lotes não enviados daquele servidor irão para a
  quarentena).
- Políticas: `UpsertItemRule`, `DeleteItemRule`, `ResolveSuspect` (permitir/bloquear).
- Quarentena: `ApplyQuarantined` (prévia; recusa se houver saldo negativo), `DiscardQuarantined`.
- Incidentes: `PreviewRollback` (o que será estornado e onde o saldo ficaria negativo, porque os
  itens já foram para outro servidor), `RevertRollback` (lançamentos compensatórios, limitados a 0,
  com a diferença registrada), `AcceptRollback`.
- Operações em dúvida: `ListDoubtful`, `RefundDoubtful` (exige lease livre), `DismissDoubtful`.
- Integração com `CreateWorldBackup` (a quente): `tccloud checkpoint` via RCON entre o
  `save-all flush` e a cópia.
- Integração com `RestoreWorldBackup`: com a nuvem ligada, ao restaurar, ler
  `world/tccloud/checkpoint.json` do zip, abrir o incidente (`Origin=Restore`) e mostrar a prévia
  no mesmo diálogo da restauração.

**Permissões** (padrão do projeto: permissão relativa ao recurso):

- Dono da nuvem (`OwnerId`) e instance admin: tudo.
- Fase posterior: `CloudVaultMembership` (`Viewer` vê saldos e auditoria; `Moderator` congela,
  resolve suspeitos e quarentena; `Owner` ajusta saldo, mexe em chaves e configurações).
- Toda ação de painel grava `CloudAdminAuditEntry`.

## 4. API HTTP (`Endpoints/CloudEndpoints.cs`)

- Grupo `/api/cloud/v1`, autenticação própria: `Authorization: Bearer tcs_<prefixo>_<segredo>` →
  busca por prefixo → compara o SHA-256 em tempo constante → põe `ServerId` e `VaultId` no
  contexto. **Não** usa o cookie de usuário.
- Rate limit por credencial (`Microsoft.AspNetCore.RateLimiting`), corpo máximo (ex.: 1 MB) e no
  máximo 500 operações por lote.
- Contrato: DTOs em `TCMine.Contracts/Cloud` com `CloudProtocol.Current`. Versão divergente →
  `426` com mensagem clara (mesma lição do `Protocol` do launcher).
- Endpoints: `hello`, `policy`, `leases/acquire`, `leases/heartbeat`, `leases/release`, `batches`,
  `channels` (POST/PATCH/DELETE), `reports/suspects`, `reports/doubtful`.
- `Background/`: um serviço que expira leases (`ExpiresAt < agora`) e, no arranque, **estende**
  todos os `Held` pelo TTL (a queda foi do TCMine, não do jogo).

## 5. Telas do painel (`Components/Pages/Cloud/`)

| Página                    | Conteúdo                                                                                                         |
|---------------------------|------------------------------------------------------------------------------------------------------------------|
| **Nuvens**                | lista das nuvens do dono, criar (com escolha lista negra/branca), totais                                         |
| **Nuvem › Servidores**    | servidores do dono, ligar/desligar, status da chave (prefixo, último contato, versão do mod), rotacionar/revogar |
| **Nuvem › Jogadores**     | busca por nome/UUID → canais → saldos (nome, ID, quantidade) → histórico do canal; congelar; ajustar com motivo  |
| **Nuvem › Regras**        | regras por item/mod/tag + fila de **suspeitos** com contagem e botões permitir/bloquear                          |
| **Nuvem › Quarentena**    | lotes com motivo, prévia do efeito, aplicar/descartar                                                            |
| **Nuvem › Incidentes**    | rollbacks detectados, prévia, reverter/aceitar                                                                   |
| **Nuvem › Em dúvida**     | operações não confirmadas após crash (jogador, item, quantidade, horário), devolver/dispensar                    |
| **Nuvem › Leases**        | quem está segurando o quê, onde e desde quando, forçar liberação                                                 |
| **Nuvem › Auditoria**     | ledger + ações de admin, filtros, exportar CSV                                                                   |
| **Nuvem › Configurações** | TTL, cotas, tamanho máximo do item, ligar/desligar                                                               |

Padrões do projeto: MudBlazor, feedback de progresso em toda ação assíncrona, confirmação em ação
destrutiva (forçar liberação, descartar, ajustar), contadores de pendências (suspeitos, quarentena,
incidentes, em dúvida) no menu.

## 6. Fases (fatias pequenas, de dentro para fora)

| Fase                                           | Entrega                                                                                                                                                                                                                                                                                                                              | Testes                                                                                                                                |
|------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| **A1. Domínio + persistência do protocolo** ✅ | `CloudVault`, `CloudServerCredential`, `CloudChannel`, `CloudItemType`, `CloudBalance`, `CloudLease` (regras de época/seq no domínio), `CloudBatch`, `CloudLedgerEntry`, `CloudQuarantine`, `GameServer.CloudVaultId`; migration `AddCloudStorage` nos dois providers                                                                | `CloudLeaseTests`, `CloudEntitiesTests`, `CloudPersistenceTests` (índice único do lote, concorrência do lease, bigint, bytes)         |
| **A2. Domínio do painel** ✅                   | `CloudItemRule`, `CloudSuspectItem`, `CloudRollbackIncident`, `CloudDoubtfulOperation`, `CloudAdminAuditEntry`; migration `AddCloudGovernance`                                                                                                                                                                                       | `CloudEntitiesTests` (padrões de regra)                                                                                               |
| **B. API do mod** ✅                           | `/api/cloud/v1`: filtro da chave (`CloudServerAuthFilter` → `AuthenticateCloudServer`), `hello`, `leases/acquire`, `leases/heartbeat`, `batches`, `leases/release`, `reports/doubtful`; limite por chave (`CloudPolicy`), corpo ≤ 2 MB; extensão dos leases no arranque (`InterruptedWorkRecovery`); `IssueCloudServerKey` (só dono) | `CloudBatchDecisionTests`, `CloudServerKeyTests`, `CloudApiContractTests` (13 cenários de ponta a ponta)                              |
| **C. Painel básico** ✅                        | `/admin/cloud` (lista, criar) e `/admin/cloud/{id}` com abas Servidores (ligar/desligar, gerar/revogar chave — mostrada uma vez), Jogadores (busca, canais, totais, lease; itens do canal; descongelar; liberar lease à força) e Configurações (nome, ligada, modo, limites); item "Nuvem de itens" no menu                          | `CloudPanelUseCasesTests` (permissões), `CloudAdminRepositoryTests` (agregações no SQLite), `CloudPanelPagesTests` (render das telas) |
| **D1. Protocolo de governança** ✅             | regras no `hello` e em `POST /policy` (o heartbeat devolve `PolicyVersion`); `reports/suspects` grava a fila; `reports/doubtful` grava com `ReportId` (reenvio não duplica); `hello` detecta mundo que voltou no tempo (só com o MESMO mundo da conexão anterior) e abre incidente → somente leitura                                 | `CloudApiContractTests` (+4)                                                                                                          |
| **D2. Painel de segurança** ✅                 | abas Regras (+ fila de suspeitos), Quarentena (aplicar/descartar), Em dúvida (devolver/dispensar), Incidentes (prévia + estornar/aceitar), Auditoria (decisões + ledger), com contagem de pendências; toda ação do painel grava `cloud_admin_audit`; backup a quente roda `tccloud checkpoint` entre o flush e a cópia               | `CloudGovernanceFlowTests` (6 cenários), `WorldBackupTests` (ordem do checkpoint), `CloudPanelPagesTests`                             |
| **E. Orquestração** ✅                         | `ProvisionServerCloudKey` no início de `StartGameServer`: chave nova (revoga a anterior) gravada em `tccloud-server.json` na pasta da instância; sem nuvem ou sem URL, o arquivo é apagado; falha aqui não impede o servidor de subir. `Server:CloudUrl` opcional (padrão: `PublicUrl`). Painel: a chave deixa de ser gerada à mão   | `ProvisionServerCloudKeyTests`, `CloudServerFilesTests`                                                                               |
| Depois                                         | `CloudVaultMembership`, ver canais no launcher (somente leitura, via hub)                                                                                                                                                                                                                                                            | —                                                                                                                                     |

## 7. Decisões e alternativas descartadas

- **Banco central único da plataforma** (em vez de cada instalação TCMine guardar a nuvem dos seus
  donos): descartado. O isolamento por dono já existe no modelo (`IOwnedEntity`), e um serviço
  central viraria ponto único de falha e de custo.
- **Saldo editável direto, sem ledger:** descartado. Sem histórico não há como investigar
  duplicação, estornar rollback nem auditar o admin.
- **Mod falando com o banco direto (Postgres):** descartado. Credencial de banco dentro de cada
  servidor de jogo, sem regra de negócio no meio.
- **Lease otimista** (deixar dois servidores mexerem e reconciliar depois): descartado. Reconciliar
  itens não tem solução sem perda ou duplicação.
- **Servidores externos** (fora do Docker do TCMine): fora da v1. Não dá para garantir
  `online-mode` nem proteger a chave.

## 8. Desvios em relação ao plano (fatia B)

- **DTOs da API em `Application/Cloud/CloudApiModels.cs`, não em `TCMine.Contracts`.** O cliente é o mod (Java), não o
  launcher; o Contracts é o que os dois produtos .NET compartilham.
- **`TimeProvider` só na nuvem**: os casos de uso da nuvem recebem `TimeProvider` (registrado como
  `TimeProvider.System`) porque a expiração do lease é regra de negócio testada avançando o relógio. O resto
  do projeto continua com `DateTimeOffset.UtcNow`.
- **Adiados para a fatia D (precisam das tabelas da A2):** detecção de rollback pelo checkpoint no `hello`
  (por ora só registrado no log) e gravação das operações em dúvida (por ora só log `Warning`).
- **Conflito ≠ erro:** só `DbUpdateConcurrencyException` e violação de índice único viram 409 ("reenvie").
  Qualquer outra falha de gravação sobe como 500 — tratá-la como conflito faria o mod reenviar para sempre.

## 9. Desvios em relação ao plano (fatia C)

- **Descongelar canal e liberar lease à força entraram já na C** (estavam na D): sem eles, um lote em
  quarentena ou um servidor morto deixariam o jogador preso até a D existir. A revisão da quarentena
  continua na D.
- **Auditoria das ações do painel por log** (`[LoggerMessage]` com o id do usuário) até a tabela
  `cloud_admin_audit` chegar com a A2.
- **Trocar ou desligar a nuvem de um servidor revoga a chave dele**: a chave vale para uma nuvem só.
- **Canal padrão = o mais antigo pela data de criação** (com o Id de desempate). O GUID v7 do .NET não é
  ordenado dentro do mesmo milissegundo; o teste pegou isso.
- **A chave é copiada à mão** para o servidor de jogo (`TCMINE_CLOUD_URL`/`TCMINE_CLOUD_KEY`, a URL é a do
  próprio painel) até a fatia E injetá-la no container.

## 10. Desvios em relação ao plano (fatia E)

- **Arquivo em vez de variável de ambiente** (decisão do autor, 2026-10-05). As variáveis de um container
  são fixadas na criação: ligar a nuvem num servidor existente ou trocar a chave exigiria recriá-lo. (Desde
  então o TCMine recria o container quando a spec muda — ver CLAUDE.md §6 —, e o argumento ficou mais forte:
  a chave nova a cada start, numa variável, recriaria o container a CADA arranque.) O arquivo `tccloud-server.json`
  (`{"url": ..., "key": ...}`) é reescrito a
  cada start, com permissão só do dono, fora de `config/` (vem do modpack) e fora do mundo (vai para os
  backups). O mod lê as variáveis de ambiente primeiro (servidores fora do TCMine) e o arquivo depois.
- **Chave nova a cada start**: o banco só guarda o hash, então reaproveitar a chave exigiria guardá-la em
  claro. Efeito colateral bom: uma chave vazada morre no próximo reinício.
- **`ONLINE_MODE` não é forçado pelo TCMine**: a imagem itzg já usa `true` por padrão e forçar mudaria
  servidores sem nuvem. Quem garante é o mod, que recusa a nuvem em modo offline.
- **Gerar chave à mão saiu do painel**: uma chave manual seria trocada no próximo start e derrubaria o
  servidor em execução. Ficou só "Revogar" (emergência).

## 11. Desvios em relação ao plano (fatias D1/D2)

- **Sem leitura do `checkpoint.json` dentro do zip no restore**: o `hello` do servidor restaurado já detecta
  o rollback (mesmo mundo, checkpoint atrás). Um caminho só, que também cobre cópia manual de mundo.
- **Aplicar quarentena ignora congelamento, cota e saldo esperado** (é exatamente o que o dono está
  decidindo); saldo negativo, canal e item desconhecidos continuam barrando.
- **Devolver operação em dúvida exige que o TCMine conheça o item**: um crédito que nunca chegou pode não
  ter mandado a definição do item; nesse caso o painel explica e só dá para dispensar.
- **Ajuste manual de saldo (`AdjustBalance`) ficou de fora**: as três decisões cobrem os casos previstos e
  um campo livre de "somar N itens" é a porta mais fácil para criar itens do nada. Fica para quando houver
  um caso real.
