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
        var repo = new FakeSettings(new InstallationSettings { CurseForgeApiKeyEncrypted = "chave-antiga" });

        await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096 }, CancellationToken.None);

        Assert.Equal("chave-antiga", repo.Salvo!.CurseForgeApiKeyEncrypted);
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
    public async Task Recusa_client_id_do_azure_que_nao_e_guid()
    {
        // Um id malformado atravessa o handshake inteiro e só falha no MSAL, na
        // máquina do jogador. Recusar aqui é a única chance de o erro aparecer
        // para quem consegue corrigi-lo.
        var result = await new UpdateSettings(new FakeSettings(new InstallationSettings()))
            .HandleAsync(
                new UpdateSettingsCommand { DefaultMemoryMb = 4096, AzureClientId = "app-do-tcmine" },
                CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Client_id_do_azure_vazio_limpa_o_valor()
    {
        // Ao contrário dos segredos acima, este campo volta preenchido para a
        // tela. Um branco aqui é decisão do admin, não "não tive como preencher"
        // — tratá-lo como "manter" deixaria o valor impossível de remover.
        var repo = new FakeSettings(new InstallationSettings
        {
            AzureClientId = "33333333-3333-3333-3333-333333333333"
        });

        var result = await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096 }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(repo.Salvo!.AzureClientId);
    }

    [Fact]
    public async Task Client_id_do_azure_e_gravado_sem_espacos()
    {
        var repo = new FakeSettings(new InstallationSettings());

        await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand
            {
                DefaultMemoryMb = 4096, AzureClientId = "  44444444-4444-4444-4444-444444444444  "
            },
            CancellationToken.None);

        Assert.Equal("44444444-4444-4444-4444-444444444444", repo.Salvo!.AzureClientId);
    }

    [Theory]
    [InlineData(25600, 25565)] // invertida
    [InlineData(80, 25599)] // porta privilegiada
    [InlineData(25565, 70000)] // além do que existe
    public async Task Recusa_faixa_de_portas_invalida(int start, int end)
    {
        // Sem a recusa, o alocador cairia na faixa padrão em silêncio e o admin
        // veria na tela um número que não vale.
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, GamePortRangeStart = start, GamePortRangeEnd = end },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
    }

    [Fact]
    public async Task Faixa_de_portas_valida_e_gravada()
    {
        var repo = new FakeSettings(new InstallationSettings());

        await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, GamePortRangeStart = 30000, GamePortRangeEnd = 30010 },
            CancellationToken.None);

        Assert.Equal(30000, repo.Salvo!.GamePortRangeStart);
        Assert.Equal(30010, repo.Salvo!.GamePortRangeEnd);
    }

    [Fact]
    public async Task Endereco_publico_e_gravado_sem_espacos()
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, PublicHost = "  jogar.exemplo.com  " },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("jogar.exemplo.com", repo.Salvo!.PublicHost);
    }

    [Fact]
    public async Task Endereco_publico_vazio_limpa_o_valor()
    {
        // Volta para a tela preenchido, então um branco é decisão do admin:
        // "voltar a usar o IP detectado".
        var repo = new FakeSettings(new InstallationSettings { PublicHost = "jogar.exemplo.com" });

        var result = await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, PublicHost = "" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(repo.Salvo!.PublicHost);
    }

    [Theory]
    [InlineData("jogar.exemplo.com:25565")] // a porta é a de cada servidor
    [InlineData("https://jogar.exemplo.com")]
    [InlineData("jogar.exemplo.com/")]
    public async Task Recusa_endereco_publico_que_nao_e_so_o_host(string publicHost)
    {
        // O que sobrasse aqui viraria parte do endereço entregue ao jogo, e o
        // erro só apareceria no jogador.
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, PublicHost = publicHost }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
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
    }
}
