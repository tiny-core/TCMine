using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.FileSystem;
using TCMine_Infrastructure.Persistence;

namespace TCMine_Infrastructure.ServerInstances;

/// <summary>
/// Monta o diretório de uma instância de servidor a partir do seu modpack, pronto para o container
/// subir. Orquestra as peças do passo de provisionamento:
///
/// <list type="number">
/// <item>garante a instalação do loader no cache compartilhado (<see cref="ServerRuntimeInstaller"/>);</item>
/// <item>liga o <c>libraries/</c> pesado do cache no diretório da instância (symlink/hardlink — sem cópia);</item>
/// <item>liga os jars dos mods do <b>lado servidor</b> (filtro <see cref="ModSideRules"/>) do cache de mods;</item>
/// <item>semeia o <c>config/</c> da instância com os overrides do modpack (cópia — editável por instância);</item>
/// <item>gera as configs (<see cref="ServerConfigWriter"/>) e marca <c>ProvisionedAt</c>.</item>
/// </list>
///
/// É idempotente: re-provisionar re-liga libraries e mods (refletindo mudanças no modpack) sem mexer no
/// <c>config/</c>/<c>world/</c> já existentes da instância (que passam a ser editados pelo painel).
/// </summary>
public sealed class ServerProvisioner(
    AppDbContext db,
    ServerRuntimeInstaller installer,
    ServerConfigWriter configWriter,
    ILinkStrategy link,
    IConfiguration config,
    IHostEnvironment env)
{
    private readonly string _root = env.ContentRootPath;

    // Imagem Docker que roda a instância; sobreponível por config. Em produção é a própria imagem do
    // release (JRE embutido); sem config, cai na oficial do temurin (pullável em dev).
    private string ImageTag => config["ServerInstances:Image"] is { Length: > 0 } img ? img : "eclipse-temurin:25-jre";

    /// <summary>
    /// Provisiona a instância <paramref name="instanceId"/>. Reporta marcos via <paramref name="progress"/>
    /// (texto curto) para o modal de progresso do painel. Persiste <c>Directory</c>, <c>ImageTag</c> e
    /// <c>ProvisionedAt</c> ao final.
    /// </summary>
    public async Task ProvisionAsync(
        Guid instanceId, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var instance = await db.ServerInstances
            .Include(i => i.Modpack!).ThenInclude(m => m.Mods).ThenInclude(mm => mm.ModFile)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new InvalidOperationException("Instância não encontrada.");

        var modpack = instance.Modpack
            ?? throw new InvalidOperationException("Instância sem modpack de origem.");

        var firstProvision = instance.ProvisionedAt is null;
        var instanceDir = Path.Combine(ServerPaths.Servers(_root), instance.Id.ToString());
        Directory.CreateDirectory(instanceDir);

        // 1. Instalação do loader no cache compartilhado (baixa+instala só na primeira vez da tupla)
        var runtime = await installer.EnsureAsync(
            modpack.Loader, modpack.LoaderVersion, modpack.Minecraft, progress, ct);
        var installDir = installer.InstalledDir(runtime);

        // 2. Liga o libraries/ do cache na instância (o grande ganho de disco)
        progress?.Report($"Ligando bibliotecas do loader ({FormatSize(runtime.SizeBytes)})…");
        LinkLibraries(installDir, instanceDir);

        // 3. Liga os jars dos mods do lado servidor (resourcepacks/shaderpacks são só cliente)
        var linked = LinkServerMods(modpack, instanceDir, progress);
        progress?.Report($"{linked} mod(s) do servidor ligados.");

        // 4. Semeia config/ com os overrides do modpack — só na primeira provisão (depois o painel manda)
        if (firstProvision)
        {
            progress?.Report("Copiando overrides do modpack…");
            var copied = SeedOverrides(modpack.Id, instanceDir);
            progress?.Report($"{copied} arquivo(s) de override copiados.");
        }

        // 5. Gera/sincroniza as configs (server.properties, eula, jvm args, listas de jogador)
        progress?.Report("Gerando configurações (server.properties, JVM, listas)…");
        configWriter.WriteAll(instanceDir, instance);

        progress?.Report("Finalizando provisionamento…");

        // Persiste o estado de provisionamento
        instance.Directory = instanceDir;
        instance.ImageTag = ImageTag;
        instance.ProvisionedAt = DateTime.UtcNow;
        instance.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ── Etapas ──────────────────────────────────────────────────────────────────────────────────────

    // Liga o libraries/ da instalação em cache para dentro da instância. Para Vanilla (sem libraries/)
    // este seria o server.jar — mas Step 2 cobre só NeoForge, que sempre produz libraries/.
    private void LinkLibraries(string installDir, string instanceDir)
    {
        var sourceLibs = Path.Combine(installDir, "libraries");
        if (!Directory.Exists(sourceLibs))
            throw new InvalidOperationException("Instalação do loader sem pasta libraries/ — install inválido.");

        link.LinkDirectory(sourceLibs, Path.Combine(instanceDir, "libraries"));
    }

    // Reconstrói a pasta mods/ da instância: liga (do cache de jars) só os mods que rodam no servidor.
    // Reporta o progresso a cada N mods (relevante com packs de 500+). Devolve quantos foram ligados.
    private int LinkServerMods(ModpackEntity modpack, string instanceDir, IProgress<string>? progress)
    {
        var modsDir = Path.Combine(instanceDir, "mods");
        // Recria do zero para refletir remoções/alterações do modpack desde a última provisão
        if (Directory.Exists(modsDir)) Directory.Delete(modsDir, true);
        Directory.CreateDirectory(modsDir);

        // Só "mod" do lado servidor entra (resourcepack/shaderpack são do cliente)
        var serverMods = modpack.Mods
            .Where(m => m.Target == "mod" && ModSideRules.RunsOnServer(m.Side) && m.ModFile is not null)
            .ToList();

        var linked = 0;
        for (var i = 0; i < serverMods.Count; i++)
        {
            var file = serverMods[i].ModFile!;
            var source = Path.Combine(ServerPaths.Mods(_root), file.FileId.ToString(), file.FileName);
            // Jar ainda não baixado no cache (ex.: mod sem download público) → ignora silenciosamente
            if (!File.Exists(source)) continue;

            link.LinkFile(source, Path.Combine(modsDir, file.FileName));
            linked++;

            // Atualiza o progresso a cada 25 mods (e no último) para não inundar a UI
            if ((i + 1) % 25 == 0 || i == serverMods.Count - 1)
                progress?.Report($"Ligando mods do servidor ({i + 1}/{serverMods.Count})…");
        }

        return linked;
    }

    // Copia os overrides do modpack (config/, scripts/, …) para a raiz da instância. Cópia (não link):
    // viram a config base editável por instância. Só roda na primeira provisão. Devolve a contagem.
    private int SeedOverrides(Guid modpackId, string instanceDir)
    {
        var overridesDir = Path.Combine(ServerPaths.Modpacks(_root), modpackId.ToString(), "overrides");
        if (!Directory.Exists(overridesDir)) return 0;

        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(overridesDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(overridesDir, file);
            var dest = Path.Combine(instanceDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
            copied++;
        }

        return copied;
    }

    // Formata bytes em MB/GB para as mensagens de progresso (ex.: tamanho do cache de libraries)
    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        var mb = bytes / (1024.0 * 1024.0);
        return mb >= 1024 ? $"{mb / 1024:0.#} GB" : $"{mb:0.#} MB";
    }
}
