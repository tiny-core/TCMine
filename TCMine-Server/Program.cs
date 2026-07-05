using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using MudBlazor.Services;
using TCMine_Server;
using TCMine_Application.Abstractions;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Identity;
using TCMine_Server.Authentication;
using TCMine_Server.Infrastructure.Persistence;
using TCMine_Server.Components;
using TCMine_Server.Components.Pages;
using TCMine_Server.Endpoints;
using TCMine_Server.Security;
using TCMine_Server.Services;
using TCMine_Server.Infrastructure.CurseForge;
using TCMine_Server.Infrastructure.Launcher;
using TCMine_Server.Infrastructure.Minecraft;
using TCMine_Server.Infrastructure.PlayerConfigs;
using TCMine_Server.Infrastructure.Server;
using TCMine_Server.Infrastructure.ServerInstances;

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
// Toda criação de pastas fica centralizada em ServerPaths (TCMine-Server.Infrastructure.FileSystem)
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

// Cache de jars/SHA-1, marcação de órfãos e upload manual. Extraído do ModpackImportService.
// Scoped: usa o AppDbContext.
builder.Services.AddScoped<ModFileCacheService>();

// Checagem de atualizações (modpack + mods) no CurseForge. Extraído do ModpackImportService.
// Scoped: usa o AppDbContext.
builder.Services.AddScoped<ModpackUpdateService>();

// Edição dos overrides do modpack (árvore de arquivos + histórico/desfazer). Extraído do
// ModpackImportService para não acumular responsabilidades. Scoped: usa o AppDbContext.
builder.Services.AddScoped<ModpackOverridesService>();

// Newsletter por modpack (CRUD direto no banco). Scoped: usa o AppDbContext.
builder.Services.AddScoped<ModpackNewsService>();

// Versões oficiais (Minecraft + loaders) para os seletores do editor. Singleton: só usa
// IHttpClientFactory + cache em memória, sem estado por requisição.
builder.Services.AddSingleton<MinecraftVersionService>();

// ── Instâncias de servidor Minecraft ─────────────────────────────────────────────────────────────────────────────────
// Estratégia de ligação de arquivos (symlink no Linux/Docker, cópia/hardlink no Windows dev),
// decidida uma vez pelo ambiente/config. Singleton: sem estado, só comportamento.
builder.Services.AddSingleton<ILinkStrategy>(LinkStrategyFactory.Create(builder.Configuration));
builder.Services.AddSingleton<ServerConfigWriter>();
// Ping de status (jogadores online) via Server List Ping. Singleton: sem estado, só I/O de rede.
builder.Services.AddSingleton<MinecraftServerPinger>();
// Ambiente Docker (client do daemon do host + tradução de path para os bind-mounts). Singleton:
// o client é thread-safe e reusado entre operações.
builder.Services.AddSingleton<DockerEnvironment>();
// Runner Java em container efêmero (instalador do loader). Singleton: sem estado por requisição.
builder.Services.AddSingleton<IServerJavaRunner, DockerServerJavaRunner>();
// Métricas ao vivo (CPU/RAM) dos containers das instâncias. Singleton: fala só com o daemon (sem BD),
// então pode ser amostrado pelo Timer do dashboard fora do circuito Blazor.
builder.Services.AddSingleton<ServerInstanceMetricsService>();
// Ciclo de vida do container do servidor (start/stop/console/logs). Scoped: usa o AppDbContext.
builder.Services.AddScoped<DockerMinecraftManager>();
// Cache de instalações de loader (dedup de disco) e provisionamento do diretório da instância.
// Scoped: usam o AppDbContext.
builder.Services.AddScoped<ServerRuntimeInstaller>();
builder.Services.AddScoped<ServerProvisioner>();
// Fachada do painel admin: CRUD das instâncias + delegação de provisão/ciclo de vida. Scoped: AppDbContext.
builder.Services.AddScoped<ServerInstanceService>();
// Coordena o provisionamento em segundo plano (fora do circuito Blazor): a página reconecta ao progresso
// após um refresh e um reinício do server retoma provisões interrompidas. Singleton: estado dos jobs.
builder.Services.AddSingleton<ProvisioningCoordinator>();
// Reconciliação periódica do status das instâncias com o daemon (detecta quedas de container).
builder.Services.AddHostedService<ServerStatusReconciler>();

// ── Usuários e autenticação por cookie ───────────────────────────────────────────────────────────────────────────────
// Usuários vivem no banco; o login valida a senha (hash) e emite um cookie de autenticação.
builder.Services.AddScoped<UserService>();
// Estado de "ocupado" do circuito: feedback bloqueante das operações async (ver BusyOverlay).
builder.Services.AddScoped<BusyService>();
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
// Recebe o dataRoot para reportar o uso do drive onde vivem os dados (tcmine-data)
builder.Services.AddSingleton(new SystemMetricsService(dataRoot));
// Feed Velopack: inspeciona tcmine-data/updates para versão/instalador do launcher
builder.Services.AddSingleton<LauncherFeedService>();
// Compila/empacota o launcher pelo servidor (dotnet publish + vpk) → feed em /updates. Singleton:
// estado do job de build (progresso reconectável, um de cada vez).
builder.Services.AddSingleton<LauncherBuildService>();
// Histórico de releases (página /admin/releases). Scoped: usa o AppDbContext.
builder.Services.AddScoped<ReleaseService>();
// Self-update: consulta as releases v* do GitHub e compara com a versão corrente. Client "github" com
// User-Agent (exigido pela API). Singleton: cache de 1h.
builder.Services.AddHttpClient("github", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine-Server");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
});
builder.Services.AddSingleton<GitHubReleaseService>();
// Auto-build: no boot / ao salvar settings, alinha o launcher à versão do servidor (se preciso).
builder.Services.AddHostedService<LauncherAutoBuildService>();

// ── Sync de conteúdo (SSE) ───────────────────────────────────────────────────────────────────────────────────────────
// Notifica os launchers ligados em /events quando o catálogo muda. Singleton: estado partilhado.
builder.Services.AddSingleton<ContentNotifier>();

// ── Configs do jogador (sync entre PCs) ──────────────────────────────────────────────────────────────────────────────
// Leitura/escrita por (uuid, modpackId) como zip em disco (tcmine-data/player-configs), servido/recebido
// por streaming direto no endpoint — sem BD (o zip pode incluir o cache do minimapa, grande).
// A validação do token Minecraft é singleton (cacheia em IMemoryCache; usa o IHttpClientFactory).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MinecraftAuthService>();
// Fachada admin para gerir/limpar as configs em disco (listar por jogador, apagar). Scoped: usa o AppDbContext.
builder.Services.AddScoped<PlayerConfigAdminService>();

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

// ── Retomada de provisões interrompidas ──────────────────────────────────────────────────────────────────────────────
// Se o server caiu no meio de um provisionamento, a instância ficou marcada como Provisioning; retoma
// agora em segundo plano. Fire-and-forget: não atrasa o boot (a query é rápida e o trabalho é assíncrono).
_ = app.Services.GetRequiredService<ProvisioningCoordinator>().RecoverAsync();

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

// ── Cabeçalhos de segurança (CSP + anti-clickjacking) ────────────────────────────────────────────────────────────────
// Cedo no pipeline para cobrir também os ficheiros estáticos abaixo. Ver TCMine_Server.Security.SecurityHeaders.
app.UseSecurityHeaders();

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
    // (mesma classificação usada pelo buffer de 404 — ver IsAssetPath, fonte única)
    var isAsset = IsAssetPath(ctx.Request.Path);

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
// Catálogo/manifesto de modpacks + serving dos jars/overrides, SSE de sync de conteúdo, configs do
// jogador e atalho de download do launcher. A API do CurseForge NÃO é exposta: o painel admin usa o
// CurseForgeApiClient in-process e o launcher baixa os jars já cacheados de /files — não há proxy /v1.
app.MapModpackEndpoints();
app.MapEventsEndpoint();
app.MapPlayerConfigEndpoints();
app.MapLauncherFeedEndpoints();
app.MapNewsEndpoints();

app.Run();

return;

// Rotas de API (consumidas pelo launcher) — devolvem o status cru, fora do buffer de 404 da UI.
static bool IsApiPath(PathString path)
{
    return path.StartsWithSegments("/api") ||
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