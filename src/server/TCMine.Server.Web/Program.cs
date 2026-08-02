using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using TCMine.Contracts.Hubs;
using TCMine.Server.Application;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Web.Background;
using TCMine.Server.Web.Components;
using TCMine.Server.Web.Configuration;
using TCMine.Server.Web.Endpoints;
using TCMine.Server.Web.Extensions;
using TCMine.Server.Web.Hubs;
using TCMine.Server.Web.Security;

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
builder.Services.AddTcMineDatabase(databaseOptions);
builder.Services.AddTcMineInfrastructure(builder.Configuration);

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

// ---------- SignalR ----------
builder.Services
    .AddSignalR(options =>
    {
        // Erro detalhado só em desenvolvimento: em produção a mensagem de
        // exceção pode revelar estrutura interna a quem está sondando.
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    })
    // MessagePack é binário: mensagens menores e serialização mais rápida
    // que JSON. Vale principalmente no stream de console, que é constante.
    .AddMessagePackProtocol();

builder.Services.AddScoped<LauncherNotifier>();
builder.Services.AddScoped<IServerHubNotifier, ServerHubNotifier>();

// ---------- Identidade (provisória) ----------
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<ICurrentUserScope, DevelopmentUserScope>();
else
    // Falha explícita e cedo. Subir em produção sem autorização real seria
    // pior do que não subir.
{
    throw new InvalidOperationException(
        "Autenticação ainda não implementada. Esta build só roda em Development.");
}

builder.Services.AddTcMineApplication();

builder.Services.AddSingleton<IngestionQueue>();
builder.Services.AddSingleton<IIngestionQueue>(sp => sp.GetRequiredService<IngestionQueue>());
builder.Services.AddHostedService<IngestionWorker>();

var app = builder.Build();

// Só em Development: aplica migrations pendentes usando a connection string
// real da App. Em produção NUNCA migramos no arranque — lá é bundle no deploy.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TcMineDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

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

app.MapHub<MainHub>(HubRoutes.Main);

app.Run();
