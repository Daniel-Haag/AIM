using Aim.Application;
using System.Security.Cryptography;
using System.Text;

namespace Aim.Infrastructure;

public sealed class RepositoryReconciler(IInvestigationStore store, InvestigationService service, string root)
{
    private readonly Dictionary<string, string> fingerprints = new();
    private int rescan;
    public void RequestRescan() => Interlocked.Exchange(ref rescan, 1);

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref rescan, 0) != 0) fingerprints.Clear();
        foreach (var investigation in store.List().Where(i => i.Root == root))
        foreach (var file in investigation.Files)
        {
            var key = investigation.Id + ":" + (OperatingSystem.IsWindows() ? file.Path.ToLowerInvariant() : file.Path);
            try
            {
                var canonical = SourcePath.Validate(root, file.Path);
                var full = Path.Combine(root, canonical);
                var hash = new StringBuilder(File.Exists(full) ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(full, cancellationToken))) : "missing");
                var references = file.References ?? file.Nodes.Where(n => n.Path != file.Path).Select(n => n.Path).ToArray();
                foreach (var candidate in references)
                {
                    hash.Append('|').Append(candidate).Append(':');
                    try
                    {
                        var target = SourcePath.Validate(root, candidate);
                        hash.Append(File.Exists(Path.Combine(root, target)) ? target : "missing");
                    }
                    catch (Exception ex) when (ex is IOException or InputException or UnauthorizedAccessException)
                    { hash.Append("unsafe"); }
                }
                var fingerprint = hash.ToString();
                if (fingerprints.TryGetValue(key, out var old) && old == fingerprint) continue;
                await service.IngestAsync(investigation.Id, new(Guid.NewGuid(), canonical, "filesystem"), cancellationToken);
                fingerprints[key] = fingerprint;
            }
            catch (Exception ex) when (ex is IOException or InputException or UnauthorizedAccessException)
            {
                if (!fingerprints.TryGetValue(key, out var old) || old != "!invalid")
                {
                    await service.InvalidateAsync(investigation.Id, file.Path, cancellationToken);
                    fingerprints[key] = "!invalid";
                }
            }
        }
    }
}
