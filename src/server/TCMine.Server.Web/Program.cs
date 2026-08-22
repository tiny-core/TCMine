using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
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
using TCMine.Server.Web.Diagnostics;
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
builder.Services.AddTcMineServerOptions(builder.Configuration, builder.Environment);

// Antes de qualquer serviço: o SQLite abre o arquivo mas não cria a pasta, e
// uma instalação nova (ou um data/ apagado) morreria aqui com uma mensagem que
// não menciona pasta nenhuma.
StoragePaths.EnsureCreated(builder.Configuration, builder.Environment);

var databaseOptions = builder.Configuration.ReadValidatedDatabaseOptions();

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

// O check do banco entra com a tag "ready": /health/live continua respondendo
// mesmo com o banco fora (o processo está vivo, não adianta o orquestrador
// reiniciá-lo), enquanto /health e /health/ready dizem a verdade.
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name, tags: [DatabaseHealthCheck.ReadyTag]);

builder.Services.AddTcMineRateLimiting();

// HSTS é grudento: o navegador guarda a promessa e recusa http naquele domínio
// até o prazo vencer. 30 dias (o padrão) é proteção real e ainda permite
// corrigir um erro de configuração dentro de um mês. Sem Preload de propósito —
// entrar na lista dos navegadores é praticamente irreversível.
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(30));

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

        // Sem este filtro a identidade da conexão não chega ao ICurrentUserScope
        // e toda checagem de papel falha — em long polling sempre, em WebSocket
        // não. Ver UserPrincipalHolder.
        options.AddFilter(new HubIdentityFilter());
    })
    // MessagePack é binário: mensagens menores e serialização mais rápida
    // que JSON. Vale principalmente no stream de console, que é constante.
    .AddMessagePackProtocol();

builder.Services.AddScoped<LauncherNotifier>();

// Singleton: um stream de console por servidor, compartilhado por todos os
// launchers que o acompanham. Scoped daria um bombeamento por invocação de hub.
builder.Services.AddSingleton<ConsoleBroadcaster>();
builder.Services.AddScoped<IServerHubNotifier, ServerHubNotifier>();

// ---------- Proteção de dados ----------
// Chaves persistidas em disco: sem isto elas são regeradas a cada arranque, o
// que derrubaria toda sessão e tornaria ilegível o que foi cifrado antes (a
// chave da API do CurseForge, a senha de SMTP).
// O caminho é configurável porque em container /app é efêmero: recriar o
// container apagaria as chaves, derrubando toda sessão e tornando ilegível o
// que foi cifrado com elas — a chave do CurseForge e a senha do SMTP.
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(StoragePaths.KeysPath(
        builder.Configuration, builder.Environment)))
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

        options.Cookie.SecurePolicy = CookiePolicyFor(builder.Environment);
    });

// O cookie de antiforgery não segue a política do cookie de sessão: o padrão do
// ASP.NET é SecurePolicy.None, e ele sai sem a marca Secure mesmo sobre https.
// Não é credencial de sessão, mas trafegar em claro num downgrade enfraquece
// justamente a proteção de CSRF que ele existe para dar.
builder.Services.AddAntiforgery(options =>
    options.Cookie.SecurePolicy = CookiePolicyFor(builder.Environment));

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<UserPrincipalHolder>();
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

// Coleta de métricas: singleton para a série sobreviver à navegação e ser a
// mesma para todos os admins abertos no painel.
builder.Services.AddSingleton<MetricsHistory>();

// Singleton e lido pela porta: a contagem de jogadores vale por uma coleta e se
// reconstitui sozinha, então não merece ida ao banco nem coluna no domínio.
builder.Services.AddSingleton<PlayerCountCache>();
builder.Services.AddSingleton<IPlayerCountSource>(sp => sp.GetRequiredService<PlayerCountCache>());
// Acerta a coluna Status com os containers que sobreviveram ao reinício.
// Registrado antes do coletor, mas sem garantia de terminar primeiro: serviços
// de background rodam concorrentes, então a primeira coleta ainda pode pular um
// servidor que só será reconhecido na seguinte, quinze segundos depois.
builder.Services.AddHostedService<ServerStatusReconciler>();

builder.Services.AddHostedService<MetricsCollector>();

// Recuperação no arranque: as filas vivem em memória, então um processo que cai
// mata o job — mas o pedido ficou gravado, e daqui ele volta para a fila.
builder.Services.AddHostedService<InterruptedWorkRecovery>();

var app = builder.Build();

// Aplica migrations pendentes no arranque.
//
// Isto valia só para Development, sob a regra de que produção migraria por
// bundle no deploy. A regra pressupõe um pipeline de deploy, e o TCMine não tem
// um: ele é entregue como imagem para alguém subir com `docker compose up` na
// própria máquina. Sem migrar aqui, o container sobe, responde ao health check
// (que não toca o banco) e devolve 500 em toda página — que é exatamente o que
// aconteceu ao testar a imagem pela primeira vez.
//
// Migrar no arranque é seguro AQUI porque a instalação é de instância única por
// natureza: o TCMine orquestra containers no Docker local, então não há réplicas
// concorrendo pela mesma migration. Quem tiver um pipeline e preferir controlar
// o momento desliga com Database:AutoMigrate=false.
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TcMineDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Banco fora do ar no arranque não pode derrubar o processo: é
        // exatamente o caso que /health/live existe para distinguir — o
        // processo está vivo, o banco não está, e reiniciar a aplicação não
        // conserta banco. Quem sobe junto com o Postgres no mesmo compose passa
        // por aqui em todo boot, enquanto o banco ainda aceita conexões.
        // O /health e o /health/ready seguem reprovando até o banco responder.
        Log.Error(ex, "Não foi possível aplicar as migrations no arranque.");
    }
}

// ---------- Pipeline ----------
app.UseForwardedHeaders();

// Depois do UseForwardedHeaders: o HstsMiddleware só emite o cabeçalho quando a
// requisição é https, e é o forwarded header que conta a verdade atrás do proxy.
// Não há UseHttpsRedirection de propósito — quem termina TLS é o proxy reverso,
// e redirecionar de dentro do container quebraria as sondas internas (health
// check do Docker bate em http no localhost) sem proteger ninguém a mais.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseTcMineSecurityHeaders();

app.UseSerilogRequestLogging();

// Depois do UseForwardedHeaders, e não antes: é ele que troca o IP do proxy pelo
// IP real do cliente. Invertida, a ordem faria todo mundo cair no mesmo balde.
app.UseRateLimiter();

app.MapHandshake();
app.MapBlobs();

// /health responde o conjunto completo de propósito: quem aponta o orquestrador
// para a URL óbvia tem de receber a resposta honesta, não a otimista. Antes daqui
// não havia check nenhum registrado, e /health devolvia 200 com o banco fora — um
// painel que não abre uma única página era reportado como saudável.
app.MapHealthChecks("/health");

// Liveness: só prova que o processo responde. Sem dependências de propósito —
// reiniciar o container não conserta um Postgres fora do ar, só derruba o painel
// junto e apaga o que estava em memória (as filas de ingestão e importação).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: pronto para receber tráfego.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DatabaseHealthCheck.ReadyTag)
});

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// MapStaticAssets (em vez de UseStaticFiles) casa com o @Assets[...] usado no
// App.razor: serve os estáticos com fingerprint por conteúdo + cache imutável.
// Assim uma mudança no app.css muda a URL e o navegador nunca serve CSS velho —
// em dev e em produção.
app.MapStaticAssets();

app.MapAuth();
app.MapLauncherAuth();
app.MapWorldBackups();

// RequireAuthorization no painel inteiro: o padrão passa a ser "precisa de
// sessão", e as exceções (login, setup) se marcam com [AllowAnonymous]. O
// inverso — proteger página a página — esquece uma cedo ou tarde.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.MapHub<MainHub>(HubRoutes.Main).RequireAuthorization();

app.Run();

// Regra única para todo cookie que a aplicação emite — sessão e antiforgery.
// Em produção o cookie NUNCA pode viajar em claro; SameAsRequest significa
// exatamente que viaja, se alguém chegar por http. Em desenvolvimento a app roda
// em http puro, e exigir Secure deixaria o login impossível de testar localmente.
static CookieSecurePolicy CookiePolicyFor(IHostEnvironment environment) =>
    environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
