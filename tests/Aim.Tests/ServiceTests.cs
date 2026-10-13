using Aim.Application;
using Aim.Domain;
using Aim.Infrastructure;
using Xunit;

namespace Aim.Tests;

public class ServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aim-service-" + Guid.NewGuid());
    private readonly SqliteInvestigationStore store;
    public ServiceTests() { Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "a.ts"), ""); store = new(Path.Combine(root, "test.db")); }
    private sealed class FixtureExtractor : IExtractor
    {
        public Task<FileSnapshot> ExtractAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileSnapshot(path, [new("a", "A", path, "module", "analyzed")], [], []));
    }
    private sealed class Notifications : IGraphNotifications { public void Publish(Guid id) { } }
    private InvestigationService Service() => new(store, new FixtureExtractor(), new Notifications(), root);

    [Fact]
    public async Task CreatesInvestigationAndRepeatedIngestionDoesNotDuplicate()
    {
        var service = Service();
        var investigation = await service.CreateAsync("A", "a.ts");
        Assert.Single(store.Get(investigation.Id)!.Files);
        var updated = await service.IngestAsync(investigation.Id, new(Guid.NewGuid(), "a.ts", "simulated-agent"));
        Assert.Equal(investigation.Revision, updated.Revision);
    }

    [Theory]
    [InlineData("../private.ts")]
    [InlineData(".env")]
    [InlineData("C:/private.ts")]
    [InlineData("node_modules/private.ts")]
    [InlineData("secrets./private.ts")]
    [InlineData("node_modules /private.ts")]
    public async Task RejectsUnauthorizedPathsBeforeExtraction(string path) =>
        await Assert.ThrowsAsync<InputException>(() => Service().CreateAsync("A", path));

    [Fact]
    public async Task RejectsUnknownEventProvenance()
    {
        var service = Service();
        var investigation = await service.CreateAsync("A", "a.ts");
        await Assert.ThrowsAsync<InputException>(() => service.IngestAsync(investigation.Id, new(Guid.NewGuid(), "a.ts", "real-codex-hook")));
    }

    [Fact]
    public async Task WindowsCasingDoesNotCreateAnotherSnapshot()
    {
        if (!OperatingSystem.IsWindows()) return;
        var service = Service();
        var investigation = await service.CreateAsync("A", "a.ts");
        var updated = await service.IngestAsync(investigation.Id, new(Guid.NewGuid(), "A.ts", "local"));
        Assert.Single(updated.Files);
        Assert.Equal(investigation.Revision, updated.Revision);
    }

    [Theory]
    [InlineData("secrets./private.ts")]
    [InlineData("node_modules /private.ts")]
    public void RejectsWindowsDirectoryAliasesBeforeCheckingExistence(string path) =>
        Assert.Throws<InputException>(() => SourcePath.Validate(root, path));

    public void Dispose() => Directory.Delete(root, true);
}
