---
type: concept
title: UX de modpacks e servidores (hub + páginas/modais)
tags: [concept, admin, blazor, ux, modpack, server-instance]
status: stable
created: 2026-06-27
updated: 2026-06-27
aliases: [hub do modpack, ModpackHub, remodelagem UX]
sources: [[[sources/2026-06-27-server-instances-e-ux]]]
related: [[[concepts/modpack-admin-editor]], [[concepts/server-instance-lifecycle]], [[concepts/async-feedback-overlay]], [[entities/tcmine-server]]]
---

# UX de modpacks e servidores (hub + páginas/modais)

> O editor de modpack em abas foi aposentado: o modpack virou um **hub overview** que liga conteúdo
> (mods/overrides/novidades/conexões) e os **servidores derivados**, com sincronização quando muda.

## O que é

Remodelagem do painel admin para criar/editar modpacks e servidores de forma mais fluida e com os dois
conceitos ligados. Substitui parte do [[concepts/modpack-admin-editor]] (as abas).

## Por que importa para o TCMine

Antes, modpack e servidor viviam em silos: criar um servidor não tocava no modpack, o modpack não sabia
das suas instâncias, e mudar o modpack não avisava os servidores. As abas afogavam tudo numa tela.

## Detalhes / Variações

### Navegação: hub + páginas/modais (sem abas)

- `/admin/modpacks/{id}` → **ModpackHub** (overview): cabeçalho + cartões (Mods, Overrides, Novidades,
  Conexões) + a lista de **servidores derivados** deste modpack.
- **Páginas próprias** para o pesado: `/{id}/mods` (o antigo editor, agora só mods + criação) e
  `/{id}/overrides`.
- **Modais** para o resto: Detalhes (metadados), Novidades, Conexões divulgadas.

### Ligação modpack ↔ servidor (bidirecional + unificação)

- O hub lista as instâncias derivadas e tem "Criar servidor deste modpack" (modpack travado).
- **Auto-divulgação**: uma instância com endereço público gera/atualiza sozinha um `ServerEntryEntity`
  (divulgado no launcher); a lista manual de "Conexões" passa a ser só servidores externos.
- **Apagar servidor é exclusivo do hub do modpack**; a lista de servidores e a tela do servidor cuidam
  de provisionar/iniciar/parar.

### Sincronização (desatualizado → aplicar atualização)

- `IsStale` = instância provisionada cujo modpack mudou desde então (`Modpack.UpdatedAt > ProvisionedAt`).
- Selo "Desatualizada" no hub, na lista de servidores, no detalhe e um aviso agregado no dashboard.
- **"Aplicar atualização"** num clique = re-provisionar **e** reiniciar (se estava rodando).

### Edição de arquivos: sempre Monaco

Padrão do projeto: editar qualquer arquivo usa o **Monaco** (BlazorMonaco) — overrides do modpack e as
configs do servidor (`server.properties`, listas de jogador, `user_jvm_args.txt`). Console do servidor
com auto-scroll para a última linha.

### Mods: compatibilidade e versão

- Ao adicionar um mod da busca, o sistema **avisa se não há versão compatível** com MC+loader e resolve
  as **dependências obrigatórias** (CurseForge) automaticamente.
- Trocar a versão de um mod é **lazy**: as versões só são buscadas ao clicar.

## Aplicação concreta

- `TCMine-Server/Components/Pages/Admin/Modpacks/`: `ModpackHub`, `OverridesPage`, `ModpackEditor`
  (agora página de mods), `Dialogs/` (Details, News, Connections, ModVersionPicker).
- `TCMine-Server/Components/Pages/Admin/Servers/`: lista, detalhe (console + Monaco), edit dialog.

## Contradições / debates conhecidos

- O ping/presença e a auto-divulgação dependem do endereço público alcançável — em DooD pode exigir
  `host.docker.internal` em vez do IP do host.

## Referências

- [[concepts/modpack-admin-editor]] (origem, abas), [[concepts/server-instance-lifecycle]],
  [[sources/2026-06-27-server-instances-e-ux]].
