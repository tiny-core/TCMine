using Microsoft.AspNetCore.Mvc;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     API que o mod <c>tccloud</c> chama de dentro de cada servidor de jogo
///     (docs/CLOUD-STORAGE.md §4). Autenticação própria: a chave do servidor no
///     cabeçalho <c>Authorization: Bearer tcs_...</c>, nunca o cookie do painel.
///     A nuvem de cada requisição sai da chave (<see cref="CloudServerAuthFilter" />),
///     nunca do corpo.
///     Códigos: 400 pedido que nunca vai passar; 401 chave; 403 servidor fora da
///     nuvem; 409 conflito de concorrência (nada gravado, reenvie); 426 protocolo.
/// </summary>
public static class CloudEndpoints
{
    /// <summary>Corpo máximo: um lote de 500 operações com definições de item cabe folgado.</summary>
    private const long MaxBodyBytes = 2 * 1024 * 1024;

    public static IEndpointRouteBuilder MapCloudApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/cloud/v1")
            .AllowAnonymous() // o cookie não vale aqui; quem autentica é o filtro
            .AddEndpointFilter<CloudServerAuthFilter>()
            .RequireRateLimiting(RateLimitPolicies.CloudPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        api.MapPost("/hello", async ([FromBody] CloudHelloRequest? request, CloudHello useCase, HttpContext http,
                CancellationToken ct) =>
            {
                if (request?.Checkpoint is null)
                    return Results.Problem("Corpo do hello incompleto.", statusCode: StatusCodes.Status400BadRequest);
                if (request.Protocol != CloudProtocol.Current)
                {
                    // 426 e não 400: o pedido está certo para outra versão. O mod
                    // mostra "atualize o mod", não "erro de comunicação".
                    return Results.Problem($"Protocolo {request.Protocol} não suportado; o TCMine fala {CloudProtocol.Current}.",
                        statusCode: StatusCodes.Status426UpgradeRequired);
                }
                return ToHttp(await useCase.HandleAsync(CloudServerAuthFilter.ContextOf(http), request, ct));
            })
            .WithName("CloudHello");

        api.MapPost("/leases/acquire", async ([FromBody] CloudAcquireRequest? request, AcquireCloudLease useCase,
                HttpContext http, CancellationToken ct) =>
            request?.PlayerUuid is null
                ? BadBody()
                : ToHttp(await useCase.HandleAsync(CloudServerAuthFilter.ContextOf(http), request, ct)))
            .WithName("CloudAcquire");

        api.MapPost("/leases/heartbeat", async ([FromBody] CloudHeartbeatRequest? request, HeartbeatCloudLeases useCase,
                HttpContext http, CancellationToken ct) =>
            request?.Leases is null
                ? BadBody()
                : ToHttp(await useCase.HandleAsync(CloudServerAuthFilter.ContextOf(http), request, ct)))
            .WithName("CloudHeartbeat");

        api.MapPost("/batches", async ([FromBody] CloudBatchRequest? request, SubmitCloudBatch useCase,
                HttpContext http, CancellationToken ct) =>
            request?.PlayerUuid is null || request.Ops is null || request.Expected is null
                ? BadBody()
                : ToHttp(await useCase.HandleAsync(CloudServerAuthFilter.ContextOf(http), request, ct)))
            .WithName("CloudBatch");

        api.MapPost("/leases/release", async ([FromBody] CloudReleaseRequest? request, ReleaseCloudLease useCase,
                HttpContext http, CancellationToken ct) =>
            request?.PlayerUuid is null
                ? BadBody()
                : ToHttp(await useCase.HandleAsync(CloudServerAuthFilter.ContextOf(http), request, ct)))
            .WithName("CloudRelease");

        api.MapPost("/reports/doubtful", ([FromBody] CloudDoubtfulRequest? request, ReportCloudDoubtful useCase,
                HttpContext http) =>
            request?.Operations is null
                ? BadBody()
                : ToHttp(useCase.Handle(CloudServerAuthFilter.ContextOf(http), request)))
            .WithName("CloudDoubtful");

        return app;
    }

    private static IResult BadBody() =>
        Results.Problem("Corpo da requisição incompleto.", statusCode: StatusCodes.Status400BadRequest);

    private static IResult ToHttp<T>(CloudCallResult<T> result) => result.Status switch
    {
        CloudCallStatus.Ok => Results.Ok(result.Value),
        CloudCallStatus.Invalid => Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest),
        CloudCallStatus.Conflict => Results.Problem(result.Error, statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(result.Error, statusCode: StatusCodes.Status403Forbidden)
    };
}

/// <summary>
///     Autentica toda requisição da API da nuvem pela chave do servidor e deixa o
///     <see cref="CloudServerContext" /> no <c>HttpContext</c>. Filtro (e não
///     esquema de autenticação do ASP.NET) porque esta identidade é de MÁQUINA e
///     só vale neste grupo: misturá-la ao cookie arriscaria uma chave de servidor
///     abrir uma página do painel.
/// </summary>
public sealed class CloudServerAuthFilter(AuthenticateCloudServer authenticate) : IEndpointFilter
{
    private const string ContextKey = "tccloud.server";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var header = http.Request.Headers.Authorization.ToString();
        var key = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..].Trim() : null;

        var result = await authenticate.HandleAsync(key, http.RequestAborted);
        if (!result.Succeeded)
            return Results.Problem(result.Error, statusCode: StatusCodes.Status401Unauthorized);

        http.Items[ContextKey] = result.Value;
        return await next(context);
    }

    public static CloudServerContext ContextOf(HttpContext http) =>
        http.Items[ContextKey] as CloudServerContext
        ?? throw new InvalidOperationException("Endpoint da nuvem sem o filtro de autenticação.");
}
