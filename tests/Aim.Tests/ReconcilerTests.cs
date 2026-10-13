using Aim.Application;
using Aim.Domain;
using Aim.Infrastructure;
using Xunit;

namespace Aim.Tests;

public class ReconcilerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aim-reconcile-" + Guid.NewGuid());
    private readonly IInvestigationStore store;
    private readonly InvestigationService service;
    private readonly RepositoryReconciler reconciler;
    private const string Source = "import { Module } from '@nestjs/common'; import { B } from './b'; @Module({imports:[B]}) export class A {}";
    public ReconcilerTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "a.ts"), Source);
        store = new SqliteInvestigationStore(Path.Combine(root, "test.db"));
        var script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../analyzer/extract.mjs"));
        service = new(store, new NodeExtractor(root, script, "node"), new GraphNotifications(), root);
        reconciler = new(store, service, root);
    }

    [Fact]
    public async Task MissingImportBecomesDependencyWithoutAFileSystemEvent()
    {
        var investigation = await service.CreateAsync("A", "a.ts");
        await reconciler.ReconcileAsync();
        Assert.Empty(GraphProjection.Build(store.Get(investigation.Id)!.Files).Edges);
        File.WriteAllText(Path.Combine(root, "b.ts"), "export class B {}");
        await reconciler.ReconcileAsync(); // No watcher was created: proves periodic recovery independently.
        Assert.Single(GraphProjection.Build(store.Get(investigation.Id)!.Files).Edges);
    }

    [Fact]
    public async Task OversizeSourceInvalidatesExistingEvidenceAndRecovers()
    {
        File.WriteAllText(Path.Combine(root, "b.ts"), "export class B {}");
        var investigation = await service.CreateAsync("A", "a.ts");
        await reconciler.ReconcileAsync();
        Assert.Single(GraphProjection.Build(store.Get(investigation.Id)!.Files).Edges);
        File.WriteAllText(Path.Combine(root, "a.ts"), new string(' ', 1024 * 1024 + 1));
        await reconciler.ReconcileAsync();
        var invalid = store.Get(investigation.Id)!;
        Assert.Empty(GraphProjection.Build(invalid.Files).Edges);
        Assert.Contains("recusada", invalid.Files[0].Diagnostics[0]);
        File.WriteAllText(Path.Combine(root, "a.ts"), Source);
        await reconciler.ReconcileAsync();
        Assert.Single(GraphProjection.Build(store.Get(investigation.Id)!.Files).Edges);
    }
    public void Dispose() => Directory.Delete(root, true);
}
