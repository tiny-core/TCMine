using NSubstitute;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public class PublishModpackVersionTests
{
    private readonly IServerHubNotifier _notifier = Substitute.For<IServerHubNotifier>();
    private readonly IModpackRepository _repo = Substitute.For<IModpackRepository>();

    private PublishModpackVersion CasoDeUso()
    {
        return new PublishModpackVersion(_repo, _notifier);
    }

    private static ModpackVersion VersaoComArquivo()
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(),
            Version = "1.0.0",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge,
            LoaderVersion = "21.1.0"
        };

        version.Files.Add(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/jei.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 1024,
            Side = FileSide.Both
        });

        return version;
    }

    [Fact]
    public async Task Publica_versao_com_arquivos()
    {
        var version = VersaoComArquivo();
        _repo.GetVersionAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);
        var ct = TestContext.Current.CancellationToken;

        var resultado = await CasoDeUso().HandleAsync(version.Id, ct);

        resultado.Succeeded.ShouldBeTrue();
        version.State.ShouldBe(ModpackVersionState.Ready);
    }

    [Fact]
    public async Task Avisa_os_launchers_ao_publicar()
    {
        var version = VersaoComArquivo();
        _repo.GetVersionAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        await CasoDeUso().HandleAsync(version.Id, TestContext.Current.CancellationToken);

        await _notifier.Received(1).NotifyModpackVersionPublishedAsync(
            version.ModpackId, version.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recusa_publicar_versao_sem_arquivos()
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(),
            Version = "1.0.0",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.Fabric,
            LoaderVersion = "0.16.0"
        };
        _repo.GetVersionAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        var resultado = await CasoDeUso().HandleAsync(version.Id, TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeFalse();
        await _notifier.DidNotReceive().NotifyModpackVersionPublishedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falha_quando_a_versao_nao_existe()
    {
        _repo.GetVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ModpackVersion?)null);

        var resultado = await CasoDeUso().HandleAsync(Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeFalse();
    }
}