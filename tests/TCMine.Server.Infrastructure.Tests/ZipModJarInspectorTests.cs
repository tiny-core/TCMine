using System.IO.Compression;
using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Infrastructure.Ingestion;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O que dá para descobrir abrindo o .jar.
///     O lado é a parte que interessa aqui, e a assimetria importa: o Fabric
///     padronizou <c>environment</c> no <c>fabric.mod.json</c>, e o NeoForge não
///     tem equivalente — o Colorwheel, que só existe para usar shaders no
///     cliente, declara todas as dependências como BOTH. Por isso o jar responde
///     para um loader e cala para o outro, e é o teste que trava essa diferença
///     antes de alguém supor que o NeoForge também responde.
/// </summary>
public sealed class ZipModJarInspectorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("client", FileSide.ClientOnly)]
    [InlineData("server", FileSide.ServerOnly)]
    [InlineData("*", FileSide.Both)]
    public async Task Le_o_lado_declarado_pelo_fabric(string environment, FileSide esperado)
    {
        var jar = Jar("fabric.mod.json", $$"""
            { "id": "sodium", "environment": "{{environment}}" }
            """);

        var info = await new ZipModJarInspector().InspectAsync(jar, Ct);

        info!.DeclaredSide.ShouldBe(esperado);
    }

    [Fact]
    public async Task Sem_environment_o_lado_fica_desconhecido()
    {
        // Nulo é "o jar não diz", e não "os dois": tratar a ausência como Both
        // é exatamente o chute que mandou mods de cliente para o servidor.
        var jar = Jar("fabric.mod.json", """{ "id": "sodium" }""");

        var info = await new ZipModJarInspector().InspectAsync(jar, Ct);

        info!.DeclaredSide.ShouldBeNull();
    }

    [Fact]
    public async Task Neoforge_nao_declara_lado()
    {
        // Este é o toml real do Colorwheel, reduzido. Um mod que serve só para
        // usar shaders no cliente, com tudo em BOTH. Se um dia o NeoForge
        // ganhar campo de lado, é este teste que vai falhar e avisar.
        var jar = Jar("META-INF/neoforge.mods.toml", """
            modLoader = "javafml"
            loaderVersion = "[4,)"

            [[mods]]
            modId = "colorwheel"
            version = "1.2.9"

            [[dependencies.colorwheel]]
            modId = "neoforge"
            type = "required"
            versionRange = "[21.1,)"
            side = "BOTH"
            """);

        var info = await new ZipModJarInspector().InspectAsync(jar, Ct);

        info!.ModId.ShouldBe("colorwheel");
        info.DeclaredSide.ShouldBeNull("o neoforge.mods.toml não tem campo de lado por mod");
    }

    private static MemoryStream Jar(string caminho, string conteudo)
    {
        var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            var entrada = zip.CreateEntry(caminho);
            using var escrita = entrada.Open();
            escrita.Write(Encoding.UTF8.GetBytes(conteudo));
        }

        buffer.Position = 0;
        return buffer;
    }
}
