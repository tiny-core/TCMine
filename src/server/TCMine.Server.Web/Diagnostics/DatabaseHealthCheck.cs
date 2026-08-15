using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Web.Diagnostics;

/// <summary>
///     O painel só está pronto se o banco responde.
///     Feito à mão, sem pacote de terceiros: o que precisamos é uma conexão de
///     verdade, e o IDbContextFactory já dá isso. AddDbContextCheck do EF exigiria
///     o contexto registrado como scoped, que é justamente o que evitamos no
///     Blazor Server.
/// </summary>
internal sealed class DatabaseHealthCheck(IDbContextFactory<TcMineDbContext> factory) : IHealthCheck
{
    public const string Name = "database";

    /// <summary>Marca os checks que respondem "pronto para receber tráfego".</summary>
    public const string ReadyTag = "ready";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);

            // CanConnectAsync abre conexão de fato; não é só olhar a connection
            // string. É o que separa "configurado" de "de pé".
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("O banco não aceitou a conexão.");
        }
        catch (Exception ex)
        {
            // Exceção aqui é resposta legítima do check, não falha da aplicação:
            // devolvemos Unhealthy em vez de deixar subir e virar 500 no endpoint.
            return HealthCheckResult.Unhealthy("Falha ao conectar ao banco.", ex);
        }
    }
}
