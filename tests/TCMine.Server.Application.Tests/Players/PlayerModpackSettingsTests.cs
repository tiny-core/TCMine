using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Players;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Players;

/// <summary>
///     As teclas que a conta leva entre PCs. A conta vem SEMPRE da sessão: um
///     id no pedido deixaria qualquer um ler (ou sobrescrever) as teclas de outra
///     pessoa.
/// </summary>
public class PlayerModpackSettingsTests
{
    private static readonly Guid PackId = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Grava_e_le_de_volta_as_teclas_da_propria_conta()
    {
        var repo = new FakeSettings();
        var scope = new FakeUserScope();

        (await Save(repo, scope).HandleAsync(PackId, "key_key.jump:key.keyboard.space", null, Ct))
            .Succeeded.ShouldBeTrue();

        var lido = await new GetPlayerModpackSettings(repo, scope).HandleAsync(PackId, Ct);

        lido.Value!.KeyBindings.ShouldBe("key_key.jump:key.keyboard.space");
        lido.Value.UserId.ShouldBe(scope.UserId!.Value);
    }

    [Fact]
    public async Task Outra_conta_nao_ve_as_teclas()
    {
        var repo = new FakeSettings();
        await Save(repo, new FakeUserScope()).HandleAsync(PackId, "key_key.jump:key.keyboard.space", null, Ct);

        var lido = await new GetPlayerModpackSettings(repo, new FakeUserScope()).HandleAsync(PackId, Ct);

        lido.Succeeded.ShouldBeTrue();
        lido.Value.ShouldBeNull();
    }

    [Fact]
    public async Task Opcoes_nulas_mantem_as_que_outro_PC_mandou()
    {
        // Um PC que só sincroniza teclas não pode apagar o resto que outro gravou.
        var repo = new FakeSettings();
        var scope = new FakeUserScope();
        var save = Save(repo, scope);

        await save.HandleAsync(PackId, "key_a", "fov:90", Ct);
        await save.HandleAsync(PackId, "key_b", null, Ct);

        var lido = (await new GetPlayerModpackSettings(repo, scope).HandleAsync(PackId, Ct)).Value!;

        lido.KeyBindings.ShouldBe("key_b");
        lido.Options.ShouldBe("fov:90");
    }

    [Fact]
    public async Task Sem_sessao_recusa()
    {
        var result = await Save(new FakeSettings(), new FakeUserScope { UserId = null })
            .HandleAsync(PackId, "key_a", null, Ct);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Teclas_grandes_demais_sao_recusadas()
    {
        var grande = new string('k', PlayerModpackSettings.MaxKeyBindingsLength + 1);

        var result = await Save(new FakeSettings(), new FakeUserScope()).HandleAsync(PackId, grande, null, Ct);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Pack_que_nao_existe_e_recusado()
    {
        var result = await new SavePlayerModpackSettings(new FakeSettings(), new Packs(exists: false), new FakeUserScope())
            .HandleAsync(PackId, "key_a", null, Ct);

        result.Succeeded.ShouldBeFalse();
    }

    private static SavePlayerModpackSettings Save(FakeSettings repo, FakeUserScope scope) =>
        new(repo, new Packs(exists: true), scope);

    private sealed class FakeSettings : IPlayerSettingsRepository
    {
        private readonly List<PlayerModpackSettings> _rows = [];

        public Task<PlayerModpackSettings?> GetAsync(Guid userId, Guid modpackId, CancellationToken ct) =>
            Task.FromResult(_rows.FirstOrDefault(r => r.UserId == userId && r.ModpackId == modpackId));

        public Task AddAsync(PlayerModpackSettings settings, CancellationToken ct)
        {
            _rows.Add(settings);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PlayerModpackSettings settings, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Packs(bool exists) : FakeModpackRepositoryBase
    {
        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(exists
                ? new Modpack { Slug = "pack", Name = "Pack", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge }
                : null);
    }
}
