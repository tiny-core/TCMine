# CLAUDE.md

Instruções para o Claude Code ao trabalhar neste repositório.

---

## Linguagem e comentários

**Comentários em PT-BR, tudo o mais em inglês.**

- Comentários `//` e `/* */` → PT-BR, explicando o *porquê* além do *o quê*
- Nomes de variáveis, propriedades, métodos, classes → inglês
- Strings e labels da UI → inglês (salvo decisão explícita do produto)

**Regra de comentários**: Como o desenvolvedor está aprendendo .NET, explique decisões não-óbvias. Comentários de bloco para classes e métodos, comentários inline para linhas não-óbvias.

Exemplo de estilo esperado:
```csharp
// Desabilita o botão durante a requisição para evitar duplo envio
private bool _loading;

// Singleton: criado uma vez e reutilizado em todo o app (sem estado por requisição)
collection.AddSingleton<SystemMetricsService>();
```

---

## Wiki do projeto (`TCMine-Docs/`)

O projeto mantém uma **base de conhecimento LLM-mantida** em `TCMine-Docs/`, no
mesmo repositório git. **Consulte o wiki antes de começar uma tarefa** e
atualize-o ao concluir mudanças significativas.

> **Onde guardar contexto/aprendizados:** TODA memória, decisão, aprendizado ou nota relevante vai no
> `TCMine-Docs/` (wiki) — **não** no sistema de auto-memória do Claude (`~/.claude/.../memory/`).
> Preferência explícita do usuário. Se algo merece ser lembrado entre sessões, escreva no wiki.

### A constituição do wiki manda

`TCMine-Docs/` tem o seu **próprio `CLAUDE.md`** (constituição), que governa
qualquer trabalho dentro daquela pasta: estrutura de três camadas (`raw/` /
`wiki/` / `CLAUDE.md`), workflows de **ingest / query / lint**, regra de idioma
(prosa PT-BR, código/identificadores em inglês) e **regra de git (não commitar
automaticamente)**. Leia-o antes de escrever no vault: `TCMine-Docs/CLAUDE.md`.

### Arquivos chave
- `TCMine-Docs/wiki/index.md` — catálogo curado de todas as páginas (entidades, conceitos, fontes)
- `TCMine-Docs/wiki/log.md` — log cronológico append-only (entrada por ingest/lint/síntese)
- `TCMine-Docs/wiki/entities/` — projetos e componentes concretos; `concepts/` — ideias/decisões transversais
- Busca: `python TCMine-Docs/tools/wikisearch.py "termos"` (BM25 sobre `wiki/**/*.md`)

### Leitura de código vivo
Para documentar código, **leia direto dos projetos reais** — não copie fonte para
`raw/`. Registre uma nota curta em `TCMine-Docs/raw/code-refs/YYYY-MM-DD-<slug>.md`
(arquivos lidos + data + takeaways) e leve o entendimento para as páginas de
`wiki/entities/` e `wiki/concepts/`.

---

## Projeto de referência

O backup em `P:\TCMine-Launcher-bk` contém a implementação completa (v1.2.0).
Use como referência de implementação, mas **reescreva de forma limpa** — não copie e cole diretamente.

Documentação do backup: `P:\TCMine-Launcher-bk\docs\`

---

## Comandos

```bash
# Build da solução inteira (TCMine.slnx)
dotnet build

# Rodar o servidor (dev → https://localhost:7002 / http://localhost:5244)
cd TCMine-Server && dotnet run

# Rodar o launcher (desktop, Avalonia)
cd TCMine-Launcher && dotnet run

# Gerar ícones/assets (launcher + servidor)
cd TCMine-IconGenerator && dotnet run

# Docker (só servidor)
docker compose up --build

# Migrations EF Core (cada provider tem o seu conjunto)
dotnet ef migrations add <Nome> --project TCMine-Infrastructure --context SqliteAppDbContext
dotnet ef migrations add <Nome> --project TCMine-Infrastructure --context PostgresAppDbContext
```

### Configuração
- **Bootstrap do banco** (fora do banco): env vars `DB_PROVIDER` (`Sqlite`/`Postgres`)
  e `DB_CONNECTION`, ou a seção `Database` do `appsettings`. SQLite é o padrão.
  Ver `appsettings.local.json` (fora do git).
- **Secrets de runtime** (token CurseForge, Azure client/tenant id, `PublicBaseUrl`):
  configurados **pelo painel admin** e guardados no banco — o token CF fica
  **cifrado** via Data Protection. **Não** há mais `CF_API_KEY`/`ADMIN_PASSWORD`
  por env var.
- **Diretórios de dados**: centralizados em `ServerPaths` sob `tcmine-data/`
  (`updates`, `secrets`, `servers`, `modpacks`, `mods`) — criados no boot.

---

## Arquitetura

Solução **.NET 10** (`TCMine.slnx`) em **Clean Architecture**. Sete projetos —
ver as páginas em `TCMine-Docs/wiki/entities/` para o detalhe de cada um.

**Core (camadas, dependências apontando para dentro):**
- **TCMine-Domain** — entidades, enums e regras puras de domínio (sem EF/ASP.NET).
- **TCMine-Application** — portas (interfaces: `ICurseForgeApi`, `IUserRepository`…),
  contratos (DTOs `record`) e lógica pura de modpack (`CurseForgeImporter`, `ModSetMerge`).
- **TCMine-Infrastructure** — EF Core (SQLite/Postgres), CurseForge, filesystem,
  identidade e serviços de servidor/Minecraft.

**Entrega e suporte:**
- **TCMine-Design** — design system compartilhado (`ColorTokens`), fonte única de
  cor para CSS/Blazor, MudBlazor e Avalonia.
- **TCMine-Server** — ASP.NET Core (Minimal API + Blazor Server). Backend que o
  launcher consome + painel admin (MudBlazor). Proxy CurseForge `/v1/*`, manifestos,
  serving de jars, SSE `/events`, feed Velopack `/updates`.
- **TCMine-Launcher** — App Avalonia 12 (WinExe), MVVM + ReactiveUI. "A Steam do TCMine".
- **TCMine-IconGenerator** — console SkiaSharp que gera ícones/favicon/og-image.

### Decisões arquiteturais chave

**Central Package Management.** Versões de NuGet centralizadas em
`Directory.Packages.props`; props comuns em `Directory.Build.props`. Uma versão
por pacote em toda a solução.

**Lógica de domínio compartilhada no core.** O que servidor e launcher precisam
decidir igual (filtro `ModSide`/`ModSideRules`, parse de loader, merge de mods,
import do CurseForge) vive em Domain/Application — não duplicado nos dois lados.

**DTOs são `record` C#.** Wire DTOs imutáveis (`TCMine-Application/Contracts`).
Nunca mudar para classes.

**Banco dual-provider.** `AppDbContext` é **abstrato**; `SqliteAppDbContext` e
`PostgresAppDbContext` concretos, cada um com suas migrations. Serviços dependem
só da base; o DI resolve a concreta pelo provider configurado.

**CurseForge sempre via proxy do servidor** (`/v1/*`), nunca direto do cliente —
o servidor injeta a `x-api-key` (que nunca sai do servidor).

**Mods servidos pelo próprio servidor.** O manifesto reescreve as URLs para
`/files/{fileId}/{fileName}`; o launcher baixa do servidor (cache em
`tcmine-data/mods`), não do CurseForge.

**SSE para sync de conteúdo.** O servidor empurra mudanças do catálogo via
`/events`; o cliente refaz a leitura ao receber uma versão maior.

**Identidade por usuários + cookie.** Sem usuário → `/setup` cria o `Owner`
(papéis `Owner/Admin/Operator/Viewer`); login emite cookie; senha em PBKDF2.
Substituiu a antiga senha única `ADMIN_PASSWORD`.

**Configs do jogador** chaveadas por `(uuid, modpackId)`, sincronizadas em
`/players/{uuid}/configs/{modpackId}` (PUT autenticado com token Minecraft).

**Velopack para auto-update** do launcher; o servidor hospeda o feed como
arquivos estáticos em `/updates`.
