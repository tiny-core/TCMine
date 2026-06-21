using Microsoft.Extensions.DependencyInjection;

namespace TCMine_Data.Authentication;

/// <summary>
/// Rastreia se o servidor já foi inicializado (existe ao menos um usuário). Singleton com
/// cache: uma vez inicializado, nunca retrocede, então evitamos bater no banco a cada
/// requisição. O middleware de primeira execução consulta isto para redirecionar a /setup.
/// </summary>
public sealed class SetupState(IServiceScopeFactory scopeFactory)
{
    // volatile: leitura/escrita simples entre threads sem lock
    private volatile bool _initialized;

    /// <summary>True se já há usuário. Consulta o banco só enquanto ainda não inicializado.</summary>
    public async ValueTask<bool> IsInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized) return true;

        // UserService é scoped — abre um escopo curto para consultar
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserService>();

        if (await users.AnyUsersExistAsync(ct))
            _initialized = true;

        return _initialized;
    }

    /// <summary>Marca como inicializado após criar o usuário master (evita nova consulta).</summary>
    public void MarkInitialized()
    {
        _initialized = true;
    }
}