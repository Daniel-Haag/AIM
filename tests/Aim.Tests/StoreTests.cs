using Aim.Domain;
using Aim.Infrastructure;
using Xunit;

namespace Aim.Tests;

public class StoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "aim-store-" + Guid.NewGuid());
    private readonly Guid id = Guid.NewGuid();
    private SqliteInvestigationStore Open() => new(Path.Combine(directory, "aim.db"));
    private static FileSnapshot Snapshot(string label = "A") => new("src/a.ts", [new("a", label, "src/a.ts", "module", "analyzed")], [], []);
    private void Create() => Open().Create(new(id, "Teste", "fixture", 0, [], []));

    [Fact]
    public void PersistsAcrossInstancesAndDuplicateEventDoesNotChangeRevisionOrHistory()
    {
        Create();
        var input = new InvestigationEvent(Guid.NewGuid(), "src/a.ts", "local");
        var first = Open().Apply(id, input, Snapshot());
        Assert.Equal(1, first.Revision);
        var second = Open().Apply(id, input, Snapshot("Must not replace"));
        Assert.Equal(1, second.Revision);
        Assert.Single(second.History);
        Assert.Equal("A", Open().Get(id)!.Files[0].Nodes[0].Label);
    }

    [Fact]
    public void UnchangedSourceIsNoOpAndChangedSourceReplacesSnapshot()
    {
        Create();
        var first = Open().Apply(id, new(Guid.NewGuid(), "src/a.ts", "local"), Snapshot());
        var same = Open().Apply(id, new(Guid.NewGuid(), "src/a.ts", "filesystem"), Snapshot());
        Assert.Equal(first.Revision, same.Revision);
        var updated = Open().Apply(id, new(Guid.NewGuid(), "src/a.ts", "simulated-agent"), Snapshot("B"));
        Assert.Equal(2, updated.Revision);
        Assert.Single(updated.Files);
        Assert.Equal("B", updated.Files[0].Nodes[0].Label);
        Assert.Equal("simulated-agent", updated.History.Last().Source);
    }

    [Fact]
    public async Task ConcurrentDuplicateEventIsAppliedOnlyOnce()
    {
        Create();
        var input = new InvestigationEvent(Guid.NewGuid(), "src/a.ts", "local");
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Open().Apply(id, input, Snapshot()))));
        Assert.Equal(1, Open().Get(id)!.Revision);
        Assert.Single(Open().Get(id)!.History);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
