using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aim.Application;
using Aim.Domain;
using Aim.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var token = Environment.GetEnvironmentVariable("AIM_TOKEN");
if (string.IsNullOrWhiteSpace(token) || token.Length < 32) throw new InvalidOperationException("Defina AIM_TOKEN com pelo menos 32 caracteres aleatórios, ou use scripts/start.ps1.");
var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
var project = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../.."));
var root = Path.GetFullPath(Environment.GetEnvironmentVariable("AIM_REPOSITORY") ?? Path.Combine(project, "fixtures/nest"));
if (!Directory.Exists(root)) throw new InvalidOperationException("Raiz autorizada não encontrada.");
SourcePath.Validate(root, "startup-check.ts");
var port = int.Parse(Environment.GetEnvironmentVariable("AIM_PORT") ?? "5187");
if (port is < 1024 or > 65535) throw new InvalidOperationException("Porta local inválida.");
var origin = Environment.GetEnvironmentVariable("AIM_UI_ORIGIN") ?? "http://127.0.0.1:5173";
if (!Uri.TryCreate(origin, UriKind.Absolute, out var uiUri) || !uiUri.IsLoopback || uiUri.Scheme != "http") throw new InvalidOperationException("A UI deve usar uma origem HTTP local.");
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origin).WithMethods("GET", "POST").WithHeaders("Authorization", "Content-Type")));
builder.Services.AddSingleton<IInvestigationStore>(_ => new SqliteInvestigationStore(Environment.GetEnvironmentVariable("AIM_DATABASE") ?? Path.Combine(project, ".local/aim.db")));
builder.Services.AddSingleton<IExtractor>(_ => new NodeExtractor(root,
    Path.GetFullPath(Environment.GetEnvironmentVariable("AIM_ANALYZER") ?? Path.Combine(project, "analyzer/extract.mjs")),
    Environment.GetEnvironmentVariable("AIM_NODE") ?? "node"));
builder.Services.AddSingleton<GraphNotifications>();
builder.Services.AddSingleton<IGraphNotifications>(sp => sp.GetRequiredService<GraphNotifications>());
builder.Services.AddSingleton(sp => new InvestigationService(sp.GetRequiredService<IInvestigationStore>(), sp.GetRequiredService<IExtractor>(), sp.GetRequiredService<IGraphNotifications>(), root));
builder.Services.AddSingleton(sp => new RepositoryReconciler(sp.GetRequiredService<IInvestigationStore>(), sp.GetRequiredService<InvestigationService>(), root));
builder.Services.AddHostedService(sp => new RepositoryWatcher(sp.GetRequiredService<RepositoryReconciler>(), root));
var app = builder.Build();
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Cache-Control"] = "no-store";
    if (context.Request.Host.Host is not ("127.0.0.1" or "localhost") ||
        (context.Request.Headers.Origin.Count > 0 && context.Request.Headers.Origin.ToString() != origin))
    { context.Response.StatusCode = 403; return; }
    try { await next(context); }
    catch (InputException ex) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = ex.Message }); }
    catch (KeyNotFoundException) { context.Response.StatusCode = 404; await context.Response.WriteAsJsonAsync(new { error = "Investigação não encontrada." }); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception) when (!context.Response.HasStarted)
    { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { error = "Falha interna. Verifique a configuração local; nenhum conteúdo de fonte foi registrado." }); }
});
app.UseCors();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var provided = context.Request.Headers.Authorization.ToString();
        if (!provided.StartsWith("Bearer ", StringComparison.Ordinal) ||
            !CryptographicOperations.FixedTimeEquals(tokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(provided[7..]))))
        { context.Response.StatusCode = 401; return; }
    }
    await next(context);
});

Investigation ReadAuthorized(IInvestigationStore store, Guid id)
{
    var investigation = store.Get(id);
    return investigation is not null && investigation.Root == root ? investigation : throw new KeyNotFoundException();
}
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/bootstrap", () => new { repository = new DirectoryInfo(root).Name, root, suggestedPath = "src/app.module.ts", integration = "local-poc" });
app.MapGet("/api/investigations", (IInvestigationStore store) => store.List().Where(i => i.Root == root).Select(i => new { i.Id, i.Name, i.Revision }));
app.MapPost("/api/investigations", async (CreateRequest input, InvestigationService service, CancellationToken ct) =>
    GraphView.From(await service.CreateAsync(input.Name, input.Path, ct)));
app.MapGet("/api/investigations/{id:guid}", (Guid id, IInvestigationStore store) => GraphView.From(ReadAuthorized(store, id)));
app.MapPost("/api/investigations/{id:guid}/events", async (Guid id, InvestigationEvent input, IInvestigationStore store, InvestigationService service, CancellationToken ct) => {
    _ = ReadAuthorized(store, id);
    if (input.Source is not ("local" or "simulated-agent")) throw new InputException("Origem externa deve ser local ou simulated-agent.");
    return GraphView.From(await service.IngestAsync(id, input, ct));
});
app.MapGet("/api/investigations/{id:guid}/stream", async (Guid id, HttpContext context, IInvestigationStore store, GraphNotifications notifications) => {
    _ = ReadAuthorized(store, id);
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers["X-Accel-Buffering"] = "no";
    var subscription = notifications.Subscribe(id);
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    try
    {
        await foreach (var _ in subscription.Reader.ReadAllAsync(context.RequestAborted))
        {
            var view = GraphView.From(ReadAuthorized(store, id));
            await context.Response.WriteAsync($"id: {view.Revision}\ndata: {JsonSerializer.Serialize(view, json)}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }
    finally { notifications.Unsubscribe(subscription.Id); }
});
app.Run();

record CreateRequest(string Name, string Path);
record GraphView(Guid Id, string Name, long Revision, Component[] Nodes, Dependency[] Edges, string[] Files, string[] Diagnostics, HistoryEntry[] History)
{
    public static GraphView From(Investigation investigation)
    {
        var graph = GraphProjection.Build(investigation.Files);
        return new(investigation.Id, investigation.Name, investigation.Revision, graph.Nodes, graph.Edges,
            investigation.Files.Select(f => f.Path).ToArray(),
            investigation.Files.SelectMany(f => f.Diagnostics.Select(d => $"{f.Path}: {d}")).ToArray(), investigation.History);
    }
}
