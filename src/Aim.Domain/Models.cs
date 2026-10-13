namespace Aim.Domain;

public sealed record Component(string Id, string Label, string Path, string Kind, string Verification);
public sealed record Evidence(string Path, int Line, int Column, string Snippet, string ContentHash);
public sealed record Dependency(string Id, string Source, string Target, string Kind, string Confidence, string Verification, Evidence Evidence);
public sealed record FileSnapshot(string Path, Component[] Nodes, Dependency[] Edges, string[] Diagnostics, string[]? References = null);
public sealed record Graph(Component[] Nodes, Dependency[] Edges);
public sealed record InvestigationEvent(Guid EventId, string Path, string Source);
public sealed record HistoryEntry(Guid EventId, string Path, string Source, DateTimeOffset At, long Revision);
public sealed record Investigation(Guid Id, string Name, string Root, long Revision, FileSnapshot[] Files, HistoryEntry[] History);

public static class GraphProjection
{
    public static Graph Build(IEnumerable<FileSnapshot> snapshots)
    {
        var files = snapshots.ToArray();
        var nodes = files.SelectMany(f => f.Nodes).GroupBy(n => n.Id)
            .Select(group => group.OrderByDescending(n => n.Verification == "analyzed").First())
            .OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
        var edges = files.SelectMany(f => f.Edges).DistinctBy(e => e.Id)
            .OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        return new(nodes, edges);
    }
}
