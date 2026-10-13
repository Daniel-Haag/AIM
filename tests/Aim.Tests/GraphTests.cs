using Aim.Domain;
using Xunit;

namespace Aim.Tests;

public class GraphTests
{
    private static readonly Component A = new("a#A", "A", "a.ts", "module", "analyzed");
    private static readonly Component B = new("b#B", "B", "b.ts", "module-reference", "referenced");
    private static readonly Dependency AB = new("ab", A.Id, B.Id, "imports", "static-syntax", "source-verified", new("a.ts", 3, 2, "B", new string('a', 64)));

    [Fact]
    public void GraphDeduplicatesNodesAndPrefersAnalyzedEvidence()
    {
        var graph = GraphProjection.Build([
            new("a.ts", [A, B], [AB], []),
            new("b.ts", [B with { Verification = "analyzed", Kind = "module" }], [], [])]);
        Assert.Equal(2, graph.Nodes.Length);
        Assert.Equal("analyzed", graph.Nodes.Single(n => n.Id == B.Id).Verification);
        Assert.Single(graph.Edges);
        Assert.Equal("a.ts", graph.Edges[0].Evidence.Path);
    }

    [Fact]
    public void ReplacingSourceSnapshotRemovesObsoleteEdgesAndOrphanReferences()
    {
        var before = GraphProjection.Build([new("a.ts", [A, B], [AB], [])]);
        Assert.Single(before.Edges);
        var after = GraphProjection.Build([new("a.ts", [A], [], [])]);
        Assert.Empty(after.Edges);
        Assert.Single(after.Nodes);
    }
}
