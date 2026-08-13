using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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
using TCMine.Server.Web.Components.Features.Servers;
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

// ---------- Proteção de dados ----------
// Chaves persistidas em disco: sem isto elas são regeradas a cada arranque, o
// que derrubaria toda sessão e tornaria ilegível o que foi cifrado antes (a
// chave da API do CurseForge, a senha de SMTP).
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "data", "keys")))
    .SetApplicationName("TCMine");

// ---------- Identidade ----------
// Cookie de sessão para o painel. O launcher usa outro caminho (handshake +
// download por hash), que segue anônimo — ver os endpoints marcados abaixo.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        // HttpOnly + SameSite=Lax: o cookie não é legível por script e não
        // acompanha requisição cross-site, o que corta CSRF e roubo por XSS.
        options.Cookie.Name = "tcmine.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserScope, HttpContextUserScope>();
builder.Services.AddScoped<ServerActions>();

builder.Services.AddTcMineApplication();

builder.Services.AddSingleton<IngestionQueue>();
builder.Services.AddSingleton<IIngestionQueue>(sp => sp.GetRequiredService<IngestionQueue>());
builder.Services.AddHostedService<IngestionWorker>();

// Registro do progresso: singleton porque o acompanhamento tem de sobreviver à
// navegação — o admin sai da página e volta sem perder o que estava vendo.
builder.Services.AddSingleton<JobProgressRegistry>();
builder.Services.AddSingleton<IJobProgressReporter>(sp => sp.GetRequiredService<JobProgressRegistry>());

builder.Services.AddSingleton<ImportQueue>();
builder.Services.AddSingleton<IImportQueue>(sp => sp.GetRequiredService<ImportQueue>());
builder.Services.AddHostedService<ImportWorker>();

// Reconciliação no arranque: as filas vivem em memória, então um processo que
// cai deixa versões presas em Resolving para sempre.
builder.Services.AddHostedService<StuckVersionReconciler>();

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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// MapStaticAssets (em vez de UseStaticFiles) casa com o @Assets[...] usado no
// App.razor: serve os estáticos com fingerprint por conteúdo + cache imutável.
// Assim uma mudança no app.css muda a URL e o navegador nunca serve CSS velho —
// em dev e em produção.
app.MapStaticAssets();

app.MapAuth();

// RequireAuthorization no painel inteiro: o padrão passa a ser "precisa de
// sessão", e as exceções (login, setup) se marcam com [AllowAnonymous]. O
// inverso — proteger página a página — esquece uma cedo ou tarde.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.MapHub<MainHub>(HubRoutes.Main).RequireAuthorization();

app.Run();
