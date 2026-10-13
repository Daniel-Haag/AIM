using Aim.Domain;

namespace Aim.Application;

public interface IExtractor
{
    Task<FileSnapshot> ExtractAsync(string path, CancellationToken cancellationToken = default);
}

public interface IInvestigationStore
{
    Investigation? Get(Guid id);
    Investigation[] List();
    void Create(Investigation investigation);
    Investigation Apply(Guid id, InvestigationEvent input, FileSnapshot snapshot);
}

public interface IGraphNotifications
{
    void Publish(Guid investigationId);
}

// Future MCP adapter maps these use cases; this is not an MCP server implementation.
public interface IInvestigationCommands
{
    Task<Investigation> CreateAsync(string name, string path, CancellationToken cancellationToken = default);
    Task<Investigation> IngestAsync(Guid id, InvestigationEvent input, CancellationToken cancellationToken = default);
}

public sealed class InputException(string message) : Exception(message);
