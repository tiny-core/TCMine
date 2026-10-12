using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Hubs;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Hubs;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Hubs;

/// <summary>
///     O aviso de mudança sai do repositório e chega ao launcher pelo hub, em
///     rajadas: uma mudança vira uma mensagem, e cem mudanças do mesmo tipo
///     também.
/// </summary>
public sealed class LauncherInvalidationsTests
{
    private static readonly Guid A = Guid.CreateVersion7();
    private static readonly Guid B = Guid.CreateVersion7();

    [Fact]
    public void Rajada_sobre_um_recurso_so_leva_o_id_dele()
    {
        LauncherInvalidations.Collapse(
                [new ResourceChange(ResourceKind.News, A), new ResourceChange(ResourceKind.News, A)])
            .ShouldBe([new ResourceChange(ResourceKind.News, A)]);
    }

    [Fact]
    public void Rajada_sobre_varios_recursos_do_mesmo_tipo_vira_um_aviso_sem_id()
    {
        // Uma importação de cem itens não pode virar cem mensagens a cada
        // launcher aberto, para o mesmo efeito: rebuscar o catálogo.
        LauncherInvalidations.Collapse(
                [new ResourceChange(ResourceKind.Modpacks, A), new ResourceChange(ResourceKind.Modpacks, B)])
            .ShouldBe([new ResourceChange(ResourceKind.Modpacks, null)]);
    }

    [Fact]
    public void Tipos_diferentes_seguem_em_avisos_separados()
    {
        LauncherInvalidations.Collapse(
                [
                    new ResourceChange(ResourceKind.Servers, A),
                    new ResourceChange(ResourceKind.Modpacks, null),
                    new ResourceChange(ResourceKind.Servers, A)
                ])
            .ShouldBe(
            [
                new ResourceChange(ResourceKind.Modpacks, null),
                new ResourceChange(ResourceKind.Servers, A)
            ]);
    }

    [Fact]
    public async Task O_aviso_chega_a_um_launcher_ligado()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var factory = new TcMineAppFactory();
        var cookie = await factory.EntrarComoAdminAsync();

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, HubRoutes.Main), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var recebido = new TaskCompletionSource<(ResourceKind Kind, Guid? Id)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Filtrado: o arranque e o login podem ter deixado outros avisos na fila,
        // e o teste é sobre ESTE.
        connection.On<ResourceKind, Guid?>(nameof(ILauncherClient.Invalidated), (kind, id) =>
        {
            if (kind is ResourceKind.News && id == A)
                recebido.TrySetResult((kind, id));
        });

        await connection.StartAsync(ct);

        factory.Services.GetRequiredService<IResourceChanges>().Touch(ResourceKind.News, A);

        var aviso = await recebido.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        aviso.ShouldBe((ResourceKind.News, A));
    }
}
