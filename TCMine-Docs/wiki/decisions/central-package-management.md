---
type: decision
title: Central Package Management
tags: [decision, build, nuget, msbuild]
status: aceita
created: 2026-06-23
updated: 2026-06-23
deciders: [Jocian]
supersedes: []
superseded-by: []
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
---

# Central Package Management

> Uma versão por pacote NuGet em toda a solução, centralizada em
> `Directory.Packages.props`.

## Contexto

Sete projetos compartilham muitas dependências (EF Core, Microsoft.Extensions,
etc.). Versões declaradas projeto a projeto divergem com o tempo e geram
conflitos sutis em runtime.

## Decisão

- Ativar **Central Package Management** (`ManagePackageVersionsCentrally=true`)
  em `Directory.Packages.props`; cada `.csproj` referencia o pacote **sem**
  `Version` (só `PackageReference Include`).
- Propriedades comuns (TargetFramework `net10.0`, `Nullable`, `ImplicitUsings`,
  `LangVersion latest`, metadados de empresa/licença) em `Directory.Build.props`.

## Consequências

- **+** Uma única fonte da verdade para versões — sem divergência entre projetos.
- **+** Atualizar um pacote = editar um arquivo.
- **−** Adicionar um pacote novo exige declarar a `PackageVersion` central antes
  de referenciá-lo (passo extra, mas explícito).

## Alternativas consideradas

- **Versões por projeto** — flexível, mas propenso a drift e conflitos
  transitivos; rejeitado.

## Referências

- `Directory.Packages.props`, `Directory.Build.props`
- [[entities/tcmine-solution]] · [[sources/2026-06-23-leitura-codigo-vivo]]
