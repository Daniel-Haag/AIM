using Aim.Domain;

namespace Aim.Application;

public sealed class InvestigationService(IInvestigationStore store, IExtractor extractor, IGraphNotifications notifications, string root) : IInvestigationCommands
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<Investigation> CreateAsync(string name, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new InputException("Nome deve ter de 1 a 100 caracteres.");
        path = SourcePath.Validate(root, path);
        if (!File.Exists(Path.Combine(root, path))) throw new InputException("Arquivo inicial não encontrado na raiz autorizada.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await extractor.ExtractAsync(path, cancellationToken);
            var investigation = new Investigation(Guid.NewGuid(), name.Trim(), root, 0, [], []);
            store.Create(investigation);
            return store.Apply(investigation.Id, new(Guid.NewGuid(), path, "local"), snapshot);
        }
        finally { gate.Release(); }
    }

    public async Task<Investigation> IngestAsync(Guid id, InvestigationEvent input, CancellationToken cancellationToken = default)
    {
        if (input.EventId == Guid.Empty || input.Source is not ("local" or "simulated-agent" or "filesystem"))
            throw new InputException("Evento exige UUID e origem local, simulated-agent ou filesystem.");
        input = input with { Path = SourcePath.Validate(root, input.Path) };
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = store.Get(id) ?? throw new KeyNotFoundException();
            if (current.Root != root) throw new InputException("Investigação pertence a outra raiz autorizada.");
            var known = current.Files.FirstOrDefault(f => SourcePath.Comparer.Equals(f.Path, input.Path));
            if (!File.Exists(Path.Combine(root, input.Path)) && known is not null) input = input with { Path = known.Path };
            if (current.Files.Length >= 64 && known is null)
                throw new InputException("Limite de 64 arquivos por investigação atingido.");
            var snapshot = await extractor.ExtractAsync(input.Path, cancellationToken);
            var updated = store.Apply(id, input, snapshot);
            if (updated.Revision != current.Revision) notifications.Publish(id);
            return updated;
        }
        finally { gate.Release(); }
    }

    // Only the local reconciler can call this; it invalidates a previously explored source without reading it.
    public async Task InvalidateAsync(Guid id, string knownPath, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = store.Get(id) ?? throw new KeyNotFoundException();
            if (current.Root != root || !current.Files.Any(f => f.Path == knownPath)) throw new InputException("Fonte não pertence ao escopo investigado.");
            var snapshot = new FileSnapshot(knownPath, [], [], ["Fonte indisponível ou recusada por limites de acesso. Relações anteriores foram invalidadas."], []);
            var updated = store.Apply(id, new(Guid.NewGuid(), knownPath, "filesystem"), snapshot);
            if (updated.Revision != current.Revision) notifications.Publish(id);
        }
        finally { gate.Release(); }
    }
}
