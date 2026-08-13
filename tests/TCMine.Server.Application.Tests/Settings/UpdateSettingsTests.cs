using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Settings;

/// <summary>
///     A regra que mais confunde aqui é "vazio = manter". A UI nunca devolve o
///     segredo atual, então um campo em branco significa "não mexi" — tratá-lo
///     como "apague" faria o admin perder a chave da API ao salvar qualquer
///     outra configuração.
/// </summary>
public sealed class UpdateSettingsTests
{
    [Fact]
    public async Task Campo_de_segredo_vazio_mantem_o_valor_atual()
    {
        var repo = new FakeSettings(new InstallationSettings
        {
            CurseForgeApiKeyEncrypted = "chave-antiga",
            SmtpPasswordEncrypted = "senha-antiga"
        });

        await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096 }, CancellationToken.None);

        Assert.Equal("chave-antiga", repo.Salvo!.CurseForgeApiKeyEncrypted);
        Assert.Equal("senha-antiga", repo.Salvo.SmtpPasswordEncrypted);
    }

    [Fact]
    public async Task Segredo_preenchido_substitui()
    {
        var repo = new FakeSettings(new InstallationSettings { CurseForgeApiKeyEncrypted = "chave-antiga" });

        await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, CurseForgeApiKey = "  chave-nova  " },
            CancellationToken.None);

        Assert.Equal("chave-nova", repo.Salvo!.CurseForgeApiKeyEncrypted);
    }

    [Fact]
    public async Task Apagar_segredo_exige_a_flag_explicita()
    {
        var repo = new FakeSettings(new InstallationSettings { CurseForgeApiKeyEncrypted = "chave-antiga" });

        await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, ClearCurseForgeApiKey = true },
            CancellationToken.None);

        Assert.Null(repo.Salvo!.CurseForgeApiKeyEncrypted);
    }

    [Fact]
    public async Task Recusa_ram_padrao_abaixo_do_minimo()
    {
        // Abaixo de 512 MB o servidor de Minecraft nem sobe.
        var result = await new UpdateSettings(new FakeSettings(new InstallationSettings()))
            .HandleAsync(new UpdateSettingsCommand { DefaultMemoryMb = 256 }, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Recusa_porta_de_smtp_invalida()
    {
        var result = await new UpdateSettings(new FakeSettings(new InstallationSettings()))
            .HandleAsync(
                new UpdateSettingsCommand { DefaultMemoryMb = 4096, SmtpPort = 70000 },
                CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    // ---- Fakes ----

    private sealed class FakeSettings(InstallationSettings settings) : ISettingsRepository
    {
        public InstallationSettings? Salvo { get; private set; }

        public Task<InstallationSettings> GetAsync(CancellationToken ct) => Task.FromResult(settings);

        public Task SaveAsync(InstallationSettings s, CancellationToken ct)
        {
            Salvo = s;
            return Task.CompletedTask;
        }

        public Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct) =>
            Task.FromResult(settings.CurseForgeApiKeyEncrypted);

        public Task<string?> GetSmtpPasswordAsync(CancellationToken ct) =>
            Task.FromResult(settings.SmtpPasswordEncrypted);
    }
}
