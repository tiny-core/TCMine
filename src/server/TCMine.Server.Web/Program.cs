using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;
using Serilog;
using TCMine.Server.Infrastructure;
using TCMine.Server.Web.Components;
using TCMine.Server.Web.Configuration;
using TCMine.Server.Web.Endpoints;
using TCMine.Server.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
// JSON compacto no stdout, sem arquivo. Em um container, log em
// arquivo cresce até encher o disco e ninguém percebe — o Docker já coleta
// o stdout e entrega a quem for agregar.
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

// ---------- Configuração ----------
builder.Services
    .AddOptions<ServerOptions>()
    .Bind(builder.Configuration.GetSection(ServerOptions.SectionName))
    .ValidateOnStart();

var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

// ---------- Serviços ----------
builder.Services.AddTCMineDatabase(databaseOptions);
builder.Services.AddTCMineInfrastructure(builder.Configuration);

// Atrás de proxy reverso (Caddy, Traefik, nginx), sem isto a aplicação vê o
// IP do proxy em vez do cliente e monta URLs com http em vez de https —
// o que quebra o SignalR e o fluxo de autenticação.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    // Em Docker o proxy está numa rede interna com IP imprevisível. Limpar
    // as listas aceita qualquer proxy — seguro apenas porque a aplicação
    // não fica exposta diretamente.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHealthChecks();

// ---------- Blazor ----------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

var app = builder.Build();

// ---------- Pipeline ----------
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();

app.MapHandshake();
app.MapBlobs();

app.MapHealthChecks("/health");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();