using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Application.Tests.Fakes;
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
    [Theory]
    [InlineData(1)]
    [InlineData(-5)]
    [InlineData(5000)]
    public async Task Prazo_do_log_de_atividade_fora_da_regra_e_recusado(int dias)
    {
        // Um prazo de 1 dia digitado por engano apagaria o feed de ontem na
        // próxima rodada; retenção não tem desfazer.
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, ActivityRetentionDays = dias }, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        repo.Salvo.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(90)]
    public async Task Prazo_do_log_de_atividade_valido_e_gravado(int dias)
    {
        var repo = new FakeSettings(new InstallationSettings());

        (await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, ActivityRetentionDays = dias },
            CancellationToken.None)).Succeeded.ShouldBeTrue();

        repo.Salvo!.ActivityRetentionDays.ShouldBe(dias);
    }

    [Fact]
    public async Task Campo_de_segredo_vazio_mantem_o_valor_atual()
    {
        var repo = new FakeSettings(new InstallationSettings { CurseForgeApiKeyEncrypted = "chave-antiga" });

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096 }, CancellationToken.None);

        Assert.Equal("chave-antiga", repo.Salvo!.CurseForgeApiKeyEncrypted);
    }

    [Fact]
    public async Task Segredo_preenchido_substitui()
    {
        var repo = new FakeSettings(new InstallationSettings { CurseForgeApiKeyEncrypted = "chave-antiga" });

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, CurseForgeApiKey = "  chave-nova  " },
            CancellationToken.None);

        Assert.Equal("chave-nova", repo.Salvo!.CurseForgeApiKeyEncrypted);
    }

    [Fact]
    public async Task Apagar_segredo_exige_a_flag_explicita()
    {
        var repo = new FakeSettings(new InstallationSettings { CurseForgeApiKeyEncrypted = "chave-antiga" });

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, ClearCurseForgeApiKey = true },
            CancellationToken.None);

        Assert.Null(repo.Salvo!.CurseForgeApiKeyEncrypted);
    }

    [Fact]
    public async Task Recusa_ram_padrao_abaixo_do_minimo()
    {
        // Abaixo de 512 MB o servidor de Minecraft nem sobe.
        var result = await new UpdateSettings(new FakeSettings(new InstallationSettings()), new FakeDnsSyncSignal())
            .HandleAsync(new UpdateSettingsCommand { DefaultMemoryMb = 256 }, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Recusa_client_id_do_azure_que_nao_e_guid()
    {
        // Um id malformado atravessa o handshake inteiro e só falha no MSAL, na
        // máquina do jogador. Recusar aqui é a única chance de o erro aparecer
        // para quem consegue corrigi-lo.
        var result = await new UpdateSettings(new FakeSettings(new InstallationSettings()), new FakeDnsSyncSignal())
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

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096 }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(repo.Salvo!.AzureClientId);
    }

    [Fact]
    public async Task Client_id_do_azure_e_gravado_sem_espacos()
    {
        var repo = new FakeSettings(new InstallationSettings());

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
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

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, GamePortRangeStart = start, GamePortRangeEnd = end },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
    }

    [Fact]
    public async Task Faixa_de_portas_valida_e_gravada()
    {
        var repo = new FakeSettings(new InstallationSettings());

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, GamePortRangeStart = 30000, GamePortRangeEnd = 30010 },
            CancellationToken.None);

        Assert.Equal(30000, repo.Salvo!.GamePortRangeStart);
        Assert.Equal(30010, repo.Salvo!.GamePortRangeEnd);
    }

    [Fact]
    public async Task Endereco_publico_e_gravado_sem_espacos()
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
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

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
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

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, PublicHost = publicHost }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
    }

    [Fact]
    public async Task Ip_local_e_gravado_sem_espacos_e_vazio_limpa()
    {
        var repo = new FakeSettings(new InstallationSettings());
        var caso = new UpdateSettings(repo, new FakeDnsSyncSignal());

        (await caso.HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, LanHost = "  192.168.1.100 " },
            CancellationToken.None)).Succeeded.ShouldBeTrue();
        repo.Salvo!.LanHost.ShouldBe("192.168.1.100");

        (await caso.HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, LanHost = "" },
            CancellationToken.None)).Succeeded.ShouldBeTrue();
        repo.Salvo!.LanHost.ShouldBeNull();
    }

    [Theory]
    [InlineData("192.168.1.100:25566")] // a porta é a de cada servidor
    [InlineData("http://192.168.1.100")]
    public async Task Recusa_ip_local_que_nao_e_so_o_host(string lanHost)
    {
        // Só quem joga em casa veria o erro, e justamente na hora de entrar.
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, LanHost = lanHost }, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        repo.Salvo.ShouldBeNull();
    }

    [Fact]
    public async Task Token_da_cloudflare_vazio_mantem_o_atual()
    {
        // A mesma regra da chave do CurseForge: o token nunca volta para a tela,
        // então um campo em branco é "não mexi".
        var repo = new FakeSettings(new InstallationSettings { CloudflareApiTokenEncrypted = "token-antigo" });

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096 }, CancellationToken.None);

        Assert.Equal("token-antigo", repo.Salvo!.CloudflareApiTokenEncrypted);
    }

    [Fact]
    public async Task Apagar_o_token_da_cloudflare_exige_a_flag_explicita()
    {
        var repo = new FakeSettings(new InstallationSettings { CloudflareApiTokenEncrypted = "token-antigo" });

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DefaultMemoryMb = 4096, ClearCloudflareApiToken = true },
            CancellationToken.None);

        Assert.Null(repo.Salvo!.CloudflareApiTokenEncrypted);
    }

    [Fact]
    public async Task Configuracao_de_dns_e_gravada_normalizada()
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand
            {
                DefaultMemoryMb = 4096,
                CloudflareApiToken = "  token-novo  ",
                CloudflareZoneId = " 0123456789abcdef0123456789ABCDEF ",
                DnsBaseDomain = " Exemplo.COM ",
                DnsHostLabel = " MC "
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("token-novo", repo.Salvo!.CloudflareApiTokenEncrypted);
        Assert.Equal("0123456789abcdef0123456789ABCDEF", repo.Salvo.CloudflareZoneId);
        Assert.Equal("exemplo.com", repo.Salvo.DnsBaseDomain);
        Assert.Equal("mc", repo.Salvo.DnsHostLabel);
    }

    [Theory]
    [InlineData("exemplo.com", null, null)] // o nome do domínio no lugar do Zone ID
    [InlineData("0123456789abcdef", null, null)] // curto
    [InlineData(null, "exemplo", null)] // um rótulo só
    [InlineData(null, "https://exemplo.com", null)]
    [InlineData(null, null, "mc.jogos")] // dois níveis
    [InlineData(null, null, "-mc")]
    public async Task Recusa_configuracao_de_dns_malformada(string? zoneId, string? baseDomain, string? hostLabel)
    {
        // Um valor errado gravado hoje vira erro da Cloudflare no dia em que
        // alguém sincronizar — longe de quem o digitou.
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand
            {
                DefaultMemoryMb = 4096,
                CloudflareZoneId = zoneId,
                DnsBaseDomain = baseDomain,
                DnsHostLabel = hostLabel
            },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
    }

    [Fact]
    public async Task Salvar_pede_a_sincronizacao_do_dns_e_recusar_nao()
    {
        // Token, zona, domínio e endereço público entram no que vai para a
        // Cloudflare: gravou, tem de sincronizar. Recusou, nada mudou.
        var signal = new FakeDnsSyncSignal();
        var settings = new UpdateSettings(new FakeSettings(new InstallationSettings()), signal);

        await settings.HandleAsync(new UpdateSettingsCommand { DefaultMemoryMb = 256 }, CancellationToken.None);
        Assert.Equal(0, signal.Requests);

        await settings.HandleAsync(new UpdateSettingsCommand { DefaultMemoryMb = 4096 }, CancellationToken.None);
        Assert.Equal(1, signal.Requests);
    }

    // ---- Modo de rede ----

    [Fact]
    public async Task Meu_dominio_sem_dominio_e_recusado()
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { NetworkMode = NetworkMode.OwnHost }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
    }

    [Fact]
    public async Task Cloudflare_incompleta_e_recusada_dizendo_o_que_falta()
    {
        // Sem isto a tela gravava "Cloudflare" e os jogadores continuavam a
        // receber o IP, porque faltava o Zone ID — e nada dizia por quê.
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { NetworkMode = NetworkMode.Cloudflare, DnsBaseDomain = "exemplo.com" },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("token", result.Error);
        Assert.Contains("Zone ID", result.Error);
        Assert.DoesNotContain("domínio", result.Error);
    }

    [Fact]
    public async Task Cloudflare_aceita_o_token_ja_gravado()
    {
        var repo = new FakeSettings(new InstallationSettings { CloudflareApiTokenEncrypted = "token" });

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand
            {
                NetworkMode = NetworkMode.Cloudflare,
                CloudflareZoneId = new string('a', 32),
                DnsBaseDomain = "exemplo.com"
            }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(NetworkMode.Cloudflare, repo.Salvo!.NetworkMode);
    }

    [Fact]
    public async Task Trocar_de_modo_guarda_os_campos_do_anterior()
    {
        // Voltar para a Cloudflare depois não pode pedir o token de novo.
        var repo = new FakeSettings(new InstallationSettings { CloudflareApiTokenEncrypted = "token" });

        await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand
            {
                NetworkMode = NetworkMode.Automatic,
                CloudflareZoneId = new string('a', 32),
                DnsBaseDomain = "exemplo.com"
            }, CancellationToken.None);

        var salvo = repo.Salvo!;
        Assert.Equal(NetworkMode.Automatic, salvo.NetworkMode);
        Assert.Equal("token", salvo.CloudflareApiTokenEncrypted);
        Assert.Equal("exemplo.com", salvo.DnsBaseDomain);
        Assert.False(salvo.DnsEnabled);
    }

    // ---- Partir do que está gravado ----

    [Fact]
    public async Task Comando_a_partir_do_gravado_nao_muda_nada()
    {
        // É o ponto de partida do assistente: ele só mexe na rede, e todo o
        // resto tem de sobreviver ao Salvar dele — os segredos inclusive, que
        // vão vazios ("manter").
        var original = new InstallationSettings
        {
            DefaultMinecraftVersion = "1.20.1",
            DefaultMemoryMb = 6144,
            WorldBackupKeepCount = 9,
            GamePortRangeStart = 30000,
            GamePortRangeEnd = 30010,
            NetworkMode = NetworkMode.OwnHost,
            PublicHost = "jogar.exemplo.com",
            LanHost = "192.168.1.100",
            CloudflareApiTokenEncrypted = "token",
            CloudflareZoneId = new string('a', 32),
            DnsBaseDomain = "exemplo.com",
            DnsSyncIntervalMinutes = 30,
            UpdateCheckDisabled = true,
            CurseForgeApiKeyEncrypted = "chave",
            AzureClientId = "55555555-5555-5555-5555-555555555555"
        };
        var repo = new FakeSettings(original);

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal())
            .HandleAsync(UpdateSettingsCommand.From(original), CancellationToken.None);

        Assert.True(result.Succeeded);
        var salvo = repo.Salvo!;
        Assert.Equal("1.20.1", salvo.DefaultMinecraftVersion);
        Assert.Equal(30000, salvo.GamePortRangeStart);
        Assert.Equal(NetworkMode.OwnHost, salvo.NetworkMode);
        Assert.Equal("jogar.exemplo.com", salvo.PublicHost);
        Assert.Equal("token", salvo.CloudflareApiTokenEncrypted);
        Assert.Equal("chave", salvo.CurseForgeApiKeyEncrypted);
        Assert.Equal(30, salvo.DnsSyncIntervalMinutes);
        Assert.True(salvo.UpdateCheckDisabled);
        Assert.Equal("55555555-5555-5555-5555-555555555555", salvo.AzureClientId);
    }

    [Fact]
    public async Task Comando_a_partir_do_gravado_corrige_o_que_estava_estragado()
    {
        // Um intervalo e uma faixa inválidos no banco não podem fazer o próprio
        // "não mude nada" ser recusado — o assistente ficaria preso.
        var original = new InstallationSettings
        {
            GamePortRangeStart = 30010,
            GamePortRangeEnd = 30000,
            DnsSyncIntervalMinutes = 0
        };

        var result = await new UpdateSettings(new FakeSettings(original), new FakeDnsSyncSignal())
            .HandleAsync(UpdateSettingsCommand.From(original), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    // ---- Túnel ----

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", null)]
    [InlineData(null, "11111111-2222-3333-4444-555555555555")]
    [InlineData("curto", "11111111-2222-3333-4444-555555555555")]
    [InlineData("0123456789abcdef0123456789abcdef", "nao-e-guid")]
    public async Task Recusa_ids_do_tunel_incompletos_ou_malformados(string? account, string? tunnel)
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { CloudflareAccountId = account, CloudflareTunnelId = tunnel },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
    }

    [Fact]
    public async Task Recusa_origem_do_tunel_com_esquema_ou_porta()
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { TunnelOriginHost = "tcp://localhost:25565" }, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Ids_do_tunel_sao_gravados()
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand
            {
                CloudflareAccountId = " 0123456789abcdef0123456789abcdef ",
                CloudflareTunnelId = "11111111-2222-3333-4444-555555555555"
            }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("0123456789abcdef0123456789abcdef", repo.Salvo!.CloudflareAccountId);
        Assert.Equal("11111111-2222-3333-4444-555555555555", repo.Salvo.CloudflareTunnelId);
    }

    // ---- Agenda do DNS ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1440)]
    public async Task Recusa_intervalo_do_dns_fora_da_lista(int minutes)
    {
        var repo = new FakeSettings(new InstallationSettings());

        var result = await new UpdateSettings(repo, new FakeDnsSyncSignal()).HandleAsync(
            new UpdateSettingsCommand { DnsSyncIntervalMinutes = minutes }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Salvo);
    }

    [Fact]
    public async Task Intervalo_e_so_manual_sao_gravados_e_acordam_o_servico()
    {
        // Acordar é o que faz o novo intervalo valer na hora: sem o pedido, o
        // serviço só releria as configurações no fim da espera antiga.
        var repo = new FakeSettings(new InstallationSettings());
        var signal = new FakeDnsSyncSignal();

        await new UpdateSettings(repo, signal).HandleAsync(
            new UpdateSettingsCommand { DnsSyncIntervalMinutes = 30, DnsManualSyncOnly = true },
            CancellationToken.None);

        Assert.Equal(30, repo.Salvo!.DnsSyncIntervalMinutes);
        Assert.True(repo.Salvo.DnsManualSyncOnly);
        Assert.Equal(1, signal.Requests);
    }

    [Theory]
    [InlineData(15, 15)]
    [InlineData(60, 60)]
    [InlineData(0, 5)]
    [InlineData(2, 5)]
    public void Intervalo_gravado_fora_da_lista_vira_o_padrao(int stored, int expected)
    {
        // Um número escrito à mão no banco não pode virar um laço de minuto em
        // minuto contra a API da Cloudflare, nem um "nunca".
        new InstallationSettings { DnsSyncIntervalMinutes = stored }.DnsSyncInterval
            .ShouldBe(TimeSpan.FromMinutes(expected));
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
