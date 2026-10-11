using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     As opções de jogo viram variáveis de ambiente do container. O que estes
///     testes prendem é a fronteira: só passa o que está no catálogo, com valor
///     válido — e nunca uma variável que o TCMine controla.
/// </summary>
public sealed class GameOptionsTests
{
    /// <summary>
    ///     Variáveis que são do TCMine ou que abrem um buraco. Nenhuma pode
    ///     aparecer no catálogo, hoje ou depois de alguém acrescentar uma opção.
    /// </summary>
    private static readonly string[] Reserved =
    [
        "TYPE", "VERSION", "FORGE_VERSION", "NEOFORGE_VERSION", "FABRIC_LOADER_VERSION", "QUILT_LOADER_VERSION",
        "EULA", "MEMORY", "INIT_MEMORY", "MAX_MEMORY", "MAX_PLAYERS", "ENABLE_RCON", "RCON_PASSWORD", "RCON_PORT",
        "SERVER_PORT", "UID", "GID", "REMOVE_OLD_MODS", "WHITELIST", "ENABLE_WHITELIST", "ENFORCE_WHITELIST",
        "ONLINE_MODE", "ENABLE_AUTOPAUSE", "ENABLE_AUTOSTOP", "JVM_OPTS", "JVM_XX_OPTS", "CUSTOM_SERVER_PROPERTIES"
    ];

    [Fact]
    public void Catalogo_nao_expoe_variavel_que_o_tcmine_controla()
    {
        var exposed = GameOptions.Catalog.Select(o => o.Key).Intersect(Reserved).ToArray();

        Assert.Empty(exposed);
    }

    [Fact]
    public void Catalogo_nao_repete_chave_e_toda_opcao_se_explica()
    {
        Assert.Equal(GameOptions.Catalog.Count, GameOptions.Catalog.Select(o => o.Key).Distinct().Count());

        Assert.All(GameOptions.Catalog, option =>
        {
            Assert.False(string.IsNullOrWhiteSpace(option.Label));
            Assert.False(string.IsNullOrWhiteSpace(option.Description));

            if (option.Kind is GameOptionKind.Choice)
                Assert.NotEmpty(option.Choices);
        });
    }

    [Fact]
    public void Valores_validos_passam_normalizados()
    {
        var (options, error) = GameOptions.Normalize(new Dictionary<string, string>
        {
            ["DIFFICULTY"] = " hard ",
            ["PVP"] = "TRUE",
            ["VIEW_DISTANCE"] = "8",
            ["MAX_TICK_TIME"] = "-1",
            ["MOTD"] = "Servidor do Jocian",
            ["TZ"] = "Europe/Lisbon"
        });

        Assert.Null(error);
        Assert.Equal("hard", options["DIFFICULTY"]);
        Assert.Equal("true", options["PVP"]);
        Assert.Equal("8", options["VIEW_DISTANCE"]);
        Assert.Equal("-1", options["MAX_TICK_TIME"]);
        Assert.Equal(6, options.Count);
    }

    [Fact]
    public void Valor_em_branco_e_opcao_nao_definida()
    {
        var (options, error) = GameOptions.Normalize(new Dictionary<string, string>
        {
            ["DIFFICULTY"] = "", ["MOTD"] = "   "
        });

        Assert.Null(error);
        Assert.Empty(options);
    }

    [Theory]
    [InlineData("VERSION", "1.21.1")] // trocaria o Minecraft por baixo do modpack
    [InlineData("ONLINE_MODE", "false")] // aceitaria contas piratas
    [InlineData("RCON_PASSWORD", "x")]
    [InlineData("QUALQUER_COISA", "1")]
    public void Chave_fora_do_catalogo_e_recusada(string key, string value)
    {
        var (_, error) = GameOptions.Normalize(new Dictionary<string, string> { [key] = value });

        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("DIFFICULTY", "impossivel")]
    [InlineData("PVP", "talvez")]
    [InlineData("VIEW_DISTANCE", "1")] // abaixo do mínimo
    [InlineData("VIEW_DISTANCE", "64")] // acima do máximo
    [InlineData("VIEW_DISTANCE", "dez")]
    [InlineData("TZ", "Europe/Lisbon; rm -rf /")]
    [InlineData("OPS", "jocian\nONLINE_MODE=false")] // uma segunda propriedade escondida numa quebra de linha
    [InlineData("MOTD", "a\nb")]
    public void Valor_invalido_e_recusado(string key, string value)
    {
        var (_, error) = GameOptions.Normalize(new Dictionary<string, string> { [key] = value });

        Assert.NotNull(error);
    }

    [Fact]
    public void Os_dois_ajustes_de_memoria_juntos_sao_recusados()
    {
        var (_, error) = GameOptions.Normalize(new Dictionary<string, string>
        {
            ["USE_AIKAR_FLAGS"] = "true", ["USE_MEOWICE_FLAGS"] = "true"
        });

        Assert.NotNull(error);
    }

    [Fact]
    public void O_que_se_grava_volta_igual()
    {
        var (options, _) = GameOptions.Normalize(new Dictionary<string, string>
        {
            ["PVP"] = "false", ["DIFFICULTY"] = "normal"
        });

        var back = GameOptions.Parse(GameOptions.Serialize(options));

        Assert.Equal("false", back["PVP"]);
        Assert.Equal("normal", back["DIFFICULTY"]);
        Assert.Equal(2, back.Count);
    }

    [Fact]
    public void Sem_opcoes_nao_se_grava_nada()
    {
        Assert.Null(GameOptions.Serialize(new Dictionary<string, string>()));
        Assert.Empty(GameOptions.Parse(null));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{\"VERSION\":\"1.21.1\",\"PVP\":\"true\"}")] // chave que não é (ou deixou de ser) do catálogo
    public void Banco_com_lixo_nao_derruba_nem_vaza_variavel(string json)
    {
        // O que está no banco também passa pelo catálogo: uma linha editada à
        // mão não vira uma variável que o TCMine controla.
        var options = GameOptions.Parse(json);

        Assert.DoesNotContain("VERSION", options.Keys);
    }
}
