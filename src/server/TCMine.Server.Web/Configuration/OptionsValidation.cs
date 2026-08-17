namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Validação da configuração no arranque.
///     A regra aqui é: erro de configuração tem de derrubar o processo na
///     partida, com a mensagem dizendo qual chave está errada. A alternativa é o
///     que tínhamos — subir "com sucesso" e falhar depois, longe da causa: um
///     PublicUrl ausente não quebra nada no servidor, só entrega a todo launcher
///     uma URL de atualização apontando para localhost. Ninguém descobre isso
///     olhando o log do painel; descobre-se dias depois, pelo cliente.
/// </summary>
public static class OptionsValidation
{
    public static IServiceCollection AddTcMineServerOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // ValidateOnStart sozinho não valida nada: ele só antecipa para o
        // arranque as validações registradas. Sem um Validate antes, a chamada é
        // decorativa — foi exatamente o caso até aqui.
        services
            .AddOptions<ServerOptions>()
            .Bind(configuration.GetSection(ServerOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Name),
                "Server:Name não pode ficar vazio — é o nome que aparece no launcher dos jogadores.")
            .Validate(
                // Em Development o fallback para localhost é aceitável e prático;
                // em produção ele é uma bomba-relógio silenciosa.
                o => environment.IsDevelopment() || o.PublicUrl is not null,
                "Server:PublicUrl é obrigatório fora de Development: é o endereço que vai no "
                + "tcmine.json e no feed de atualização do launcher. Sem ele, os clientes "
                + "recebem 'https://localhost'.")
            .Validate(
                o => o.PublicUrl is null
                     || (o.PublicUrl.IsAbsoluteUri && o.PublicUrl.Scheme is "http" or "https"),
                "Server:PublicUrl precisa ser uma URL absoluta http/https — o jogador alcança "
                + "este endereço de fora, não é o IP interno do container.")
            .Validate(
                // Mesma lógica do PublicUrl: em Development ninguém entra pelo
                // launcher, mas em produção este campo vazio significa que o
                // jogador abre o launcher e não tem contra o que autenticar. O
                // servidor sobe saudável e o sintoma aparece só na máquina dele.
                o => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(o.AzureClientId),
                "Server:AzureClientId é obrigatório fora de Development: é o client id da app "
                + "Azure que o launcher usa para o login com a Microsoft. Sem ele nenhum "
                + "jogador consegue entrar.")
            .Validate(
                // String vazia não é o mesmo que ausente: vazia o launcher recebe
                // e tenta comparar; ausente ele entende como "sem mínimo".
                o => o.MinLauncherVersion is null || !string.IsNullOrWhiteSpace(o.MinLauncherVersion),
                "Server:MinLauncherVersion está em branco. Remova a chave em vez de deixá-la "
                + "vazia, senão o launcher recebe um mínimo que não sabe interpretar.")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    ///     Lê e valida a seção Database.
    ///     Fora do sistema de Options porque o valor é preciso durante o registro
    ///     dos serviços, antes de existir um provider para resolvê-lo. A validação
    ///     lança na cara do arranque, que é o efeito que queremos de qualquer
    ///     forma.
    ///     Sem isto, um Provider com erro de digitação só estoura na PRIMEIRA
    ///     consulta ao banco — o switch que escolhe o provider roda quando o
    ///     contexto é criado, não quando é registrado. A aplicação sobe, responde
    ///     ao health check e quebra na primeira tela que alguém abrir.
    /// </summary>
    public static DatabaseOptions ReadValidatedDatabaseOptions(this IConfiguration configuration)
    {
        var options = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        if (options.Provider is not ("Postgres" or "Sqlite"))
        {
            throw new InvalidOperationException(
                $"Database:Provider inválido: '{options.Provider}'. Use 'Postgres' ou 'Sqlite'.");
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Database:ConnectionString não pode ficar vazia.");
        }

        return options;
    }
}
