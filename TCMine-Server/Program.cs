using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using MudBlazor.Services;
using TCMine_Server;
using TCMine_Application.Abstractions;
using TCMine_Infrastructure.FileSystem;
using TCMine_Infrastructure.Identity;
using TCMine_Server.Authentication;
using TCMine_Infrastructure.Persistence;
using TCMine_Server.Components;
using TCMine_Server.Components.Pages;
using TCMine_Server.Endpoints;
using TCMine_Infrastructure.CurseForge;
using TCMine_Infrastructure.Launcher;
using TCMine_Infrastructure.Minecraft;
using TCMine_Infrastructure.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

// ── Blazor + MudBlazor ───────────────────────────────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// appsettings.local.json — config local fora do git. Hoje só guarda o BOOTSTRAP do banco
// (DB_PROVIDER/DB_CONNECTION):
builder.Configuration
    .AddJsonFile("appsettings.local.json", true, true)
    .AddEnvironmentVariables();

// ── Diretórios de dados ──────────────────────────────────────────────────────────────────────────────────────────────
// Toda criação de pastas fica centralizada em ServerPaths (TCMine-Infrastructure.FileSystem)
var dataRoot = builder.Environment.ContentRootPath;
ServerPaths.EnsureCreated(dataRoot);

// ── Camada de dados (EF Core) ────────────────────────────────────────────────────────────────────────────────────────
// Provider (SQLite/Postgres) e connection string vêm da config; ver DatabaseServiceCollectionExtensions.
builder.Services.AddTcMineDatabase(builder.Configuration);

// ── Data Protection (cifra de segredos em repouso) ───────────────────────────────────────────────────────────────────
// As chaves ficam em disco (tcmine-data/secrets) para sobreviver a restart e ao Docker.
// SetApplicationName fixa o isolamento — chaves só valem para esta aplicação.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(ServerPaths.Secrets(dataRoot)))
    .SetApplicationName("TCMine-Server");

// Settings de runtime (token CurseForge, Azure client/tenant id) lidas/gravadas no banco.
builder.Services.AddSingleton<ServerSettingsService>();

// ── CurseForge ───────────────────────────────────────────────────────────────────────────────────────────────────────
// HttpClient nomeado com a base da API oficial; a x-api-key é injetada por requisição
// (o token vem das settings cifradas e pode mudar em runtime). O launcher nunca chega aqui.
builder.Services.AddHttpClient(CurseForgeApiClient.HttpClientName,
    http => { http.BaseAddress = new Uri("https://api.curseforge.com/"); });

// Client concreto registrado também por si — o painel usa a busca (search/files), além da interface
// consumida pelo importador. Uma instância por escopo serve aos dois.
builder.Services.AddScoped<CurseForgeApiClient>();
builder.Services.AddScoped<ICurseForgeApi>(sp => sp.GetRequiredService<CurseForgeApiClient>());

// Import e manutenção de modpacks (baixa jars, infere Side, persiste). Scoped: usa o AppDbContext.
builder.Services.AddScoped<ModpackImportService>();

// Versões oficiais (Minecraft + loaders) para os seletores do editor. Singleton: só usa
// IHttpClientFactory + cache em memória, sem estado por requisição.
builder.Services.AddSingleton<MinecraftVersionService>();

// ── Usuários e autenticação por cookie ───────────────────────────────────────────────────────────────────────────────
// Usuários vivem no banco; o login valida a senha (hash) e emite um cookie de autenticação.
builder.Services.AddScoped<UserService>();
// Detecção de primeira execução (existe algum usuário?) — singleton com cache.
builder.Services.AddSingleton<SetupState>();
builder.Services.AddSingleton<IValidator<Login.LoginInput>, Login.InputValidator>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "tcmine_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        // Sem esquema próprio de acesso negado: o AdminLayout já mostra a tela 403
        options.AccessDeniedPath = "/admin";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// O estado de auth dos componentes vem do cookie (prerender) e é persistido para o circuito.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

// ── Conteúdo e métricas ──────────────────────────────────────────────────────────────────────────────────────────────
// Singletons: sem estado por requisição (catálogo em memória; métricas medem o processo)
builder.Services.AddSingleton<ContentCatalog>();
builder.Services.AddSingleton<SystemMetricsService>();
// Feed Velopack: inspeciona tcmine-data/updates para versão/instalador do launcher
builder.Services.AddSingleton<LauncherFeedService>();

// ── Sync de conteúdo (SSE) ───────────────────────────────────────────────────────────────────────────────────────────
// Notifica os launchers ligados em /events quando o catálogo muda. Singleton: estado partilhado.
builder.Services.AddSingleton<ContentNotifier>();

// ── Configs do jogador (sync entre PCs) ──────────────────────────────────────────────────────────────────────────────
// Leitura/escrita por (uuid, modpackId) via IPlayerConfigRepository (registrado em AddTcMineDatabase).
// A validação do token Minecraft é singleton (cacheia em IMemoryCache; usa o IHttpClientFactory).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MinecraftAuthService>();

// ── Rate limiting dos endpoints públicos (por IP) ────────────────────────────────────────────────────────────────────
// Protege o PUT de configs do jogador contra abuso (o proxy CurseForge entra aqui no futuro).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("configs", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1), PermitLimit = 30, QueueLimit = 0
            }));
});

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ── Migrations ───────────────────────────────────────────────────────────────────────────────────────────────────────
// Aplica as migrations pendentes no boot. Resolve o AppDbContext já registrado (o DI escolheu a
// subclasse concreta conforme o provider). Sem isto, o schema nunca é criado/atualizado.
await app.Services.MigrateTcMineDatabaseAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(handler =>
    {
        handler.Run(ctx =>
        {
            try
            {
                var feature = ctx.Features.Get<IExceptionHandlerFeature>();
                if (feature?.Error is { } ex)
                {
                    var logger = ctx.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("TCMine.Errors");

                    // Loga com o stack trace completo para facilitar o diagnóstico
                    logger.LogError(ex, "Exceção não tratada em {Method} {Path}",
                        ctx.Request.Method, ctx.Request.Path);
                }

                ctx.Response.Redirect("/Error");
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        });
    });

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// 404 estilizado para navegações de página: a rota catch-all de NotFound.razor renderiza o corpo
// (a 200, porque definir 404 no render SSR do Blazor descarta a saída) e marca o HttpContext. Este
// middleware faz buffer da resposta da página e promove o status a 404 antes de a enviar — assim o
// corpo é preservado e o status fica correto. Restrito a GET de página: rotas de API (status cru ao
// launcher), SSE (/events) e ficheiros estáticos ficam de fora (não podem ser bufferizados).
app.UseWhen(
    ctx => HttpMethods.IsGet(ctx.Request.Method)
           && !IsApiPath(ctx.Request.Path)
           && !IsAssetPath(ctx.Request.Path),
    branch => branch.Use(async (ctx, next) =>
    {
        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try
        {
            await next();

            // A página catch-all marcou o contexto → promove a 404 (resposta ainda em buffer).
            if (ctx.Items.ContainsKey(NotFoundResponseMarker.Key) && !ctx.Response.HasStarted)
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;

            ctx.Response.Body = originalBody;
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
        }
        finally
        {
            ctx.Response.Body = originalBody;
        }
    }));

app.UseHttpsRedirection();

// ── Feed Velopack (arquivos estáticos em /updates) ──────────────────────────────────────────────────────────────────
// O autoupdate do launcher consome RELEASES/nupkg/Setup.exe daqui. Servido cedo no pipeline (antes
// de auth e do redirect de primeira execução) para ficar sempre acessível.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(ServerPaths.Updates(dataRoot)),
    RequestPath = "/updates",
    ServeUnknownFileTypes = true // RELEASES não tem extensão; o Velopack precisa de o ler
});

app.UseRateLimiter();

// Autenticação/autorização por cookie — popula HttpContext.User antes dos componentes.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();


// ── Primeira execução: sem usuários → força o setup do usuário master ────────────────────────────────────────────────
// Enquanto não houver usuário, qualquer rota (exceto assets e o próprio /setup) vai para /setup.
// Após inicializado, /setup deixa de existir (volta ao login).
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "/";

    // Deixa passar assets do framework (/_framework, /_blazor), conteúdo estático e arquivos
    var isAsset = path.StartsWith("/_", StringComparison.Ordinal)
                  || path.StartsWith("/css", StringComparison.Ordinal)
                  || path.StartsWith("/js", StringComparison.Ordinal)
                  || path.StartsWith("/images", StringComparison.Ordinal)
                  || path.StartsWith("/favicon", StringComparison.Ordinal)
                  || Path.HasExtension(path);

    // Páginas utilitárias do framework têm de renderizar mesmo antes do setup. Sem isto, o
    // re-execute de /not-found (StatusCodePages) e a página /Error seriam apanhados pelo redirect
    // para /setup — o fallback de 404 viraria um 302 e nunca apareceria.
    var isFrameworkPage = path.Equals("/not-found", StringComparison.OrdinalIgnoreCase)
                          || path.Equals("/Error", StringComparison.OrdinalIgnoreCase);

    if (!isAsset && !isFrameworkPage)
    {
        var setup = ctx.RequestServices.GetRequiredService<SetupState>();
        var initialized = await setup.IsInitializedAsync(ctx.RequestAborted);

        switch (initialized)
        {
            case false when path != "/setup":
                ctx.Response.Redirect("/setup");
                return;
            case true when path == "/setup":
                ctx.Response.Redirect("/login");
                return;
        }
    }

    await next();
});

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Logout: limpa o cookie e volta ao login. GET por simplicidade (navegação com reload completo).
app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// ── Endpoints do launcher ────────────────────────────────────────────────────────────────────────────────────────────
// Catálogo/manifesto de modpacks + serving dos jars/overrides, proxy CurseForge, SSE de sync de
// conteúdo, configs do jogador e atalho de download do launcher.
app.MapModpackEndpoints();
app.MapCurseForgeProxy();
app.MapEventsEndpoint();
app.MapPlayerConfigEndpoints();
app.MapLauncherFeedEndpoints();

app.Run();

return;

// Rotas de API (consumidas pelo launcher) — devolvem o status cru, fora do buffer de 404 da UI.
static bool IsApiPath(PathString path)
{
    return path.StartsWithSegments("/api") || path.StartsWithSegments("/v1") ||
           path.StartsWithSegments("/files") || path.StartsWithSegments("/players") ||
           path.StartsWithSegments("/events") || path.StartsWithSegments("/download") ||
           path.StartsWithSegments("/updates");
}

// Assets do framework/estáticos — não são bufferizados (poupa cópia e preserva streaming de arquivos).
static bool IsAssetPath(PathString path)
{
    var p = path.Value ?? "/";
    return p.StartsWith("/_", StringComparison.Ordinal)
           || p.StartsWith("/css", StringComparison.Ordinal)
           || p.StartsWith("/js", StringComparison.Ordinal)
           || p.StartsWith("/images", StringComparison.Ordinal)
           || p.StartsWith("/favicon", StringComparison.Ordinal)
           || Path.HasExtension(p);
}