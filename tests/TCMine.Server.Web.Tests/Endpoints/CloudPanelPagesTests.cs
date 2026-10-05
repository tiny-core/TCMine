using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     As telas da nuvem renderizam de verdade (o Blazor pré-renderiza no GET):
///     pega componente que quebra ao abrir e caso de uso que não foi registrado.
/// </summary>
public sealed class CloudPanelPagesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lista_e_pagina_da_nuvem_abrem_para_o_dono()
    {
        await using var factory = new TcMineAppFactory();
        var cookie = await factory.EntrarComoAdminAsync();
        var vaultId = await CriarNuvemDoAdminAsync(factory, "Nuvem do Survival");

        var lista = await GetAsync(factory, cookie, "/admin/cloud");
        lista.ShouldContain("Nuvem de itens");
        lista.ShouldContain("Nuvem do Survival");

        var pagina = await GetAsync(factory, cookie, $"/admin/cloud/{vaultId}");
        pagina.ShouldContain("Nuvem do Survival");
        pagina.ShouldContain("Servidores");
        pagina.ShouldContain("Jogadores");
        pagina.ShouldContain("Quarentena");
        pagina.ShouldContain("Incidentes");
    }

    [Fact]
    public async Task Nuvem_inexistente_mostra_nao_encontrada()
    {
        await using var factory = new TcMineAppFactory();
        var cookie = await factory.EntrarComoAdminAsync();

        var pagina = await GetAsync(factory, cookie, $"/admin/cloud/{Guid.CreateVersion7()}");

        pagina.ShouldContain("Nuvem não encontrada.");
    }

    private static async Task<Guid> CriarNuvemDoAdminAsync(TcMineAppFactory factory, string nome)
    {
        var dbs = factory.Services.GetRequiredService<IDbContextFactory<TcMineDbContext>>();
        await using var db = await dbs.CreateDbContextAsync(Ct);
        var admin = await db.Users.SingleAsync(u => u.IsInstanceAdmin, Ct);
        var nuvem = new CloudVault { Name = nome, OwnerId = admin.Id };
        db.CloudVaults.Add(nuvem);
        await db.SaveChangesAsync(Ct);
        return nuvem.Id;
    }

    private static async Task<string> GetAsync(TcMineAppFactory factory, string cookie, string url)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));
    }
}
