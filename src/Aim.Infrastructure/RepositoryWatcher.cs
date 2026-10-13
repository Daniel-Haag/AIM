using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace Aim.Infrastructure;

// Reconcile only explicitly explored files. Never enumerate/read the entire repository.
public sealed class RepositoryWatcher(RepositoryReconciler reconciler, string root) : BackgroundService
{
    private readonly Channel<bool> changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var watcher = new FileSystemWatcher(root, "*.ts") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
        watcher.Changed += (_, _) => changes.Writer.TryWrite(true);
        watcher.Created += (_, _) => changes.Writer.TryWrite(true);
        watcher.Deleted += (_, _) => changes.Writer.TryWrite(true);
        watcher.Renamed += (_, _) => changes.Writer.TryWrite(true);
        watcher.Error += (_, _) => { reconciler.RequestRescan(); changes.Writer.TryWrite(true); };
        watcher.EnableRaisingEvents = true;
        changes.Writer.TryWrite(true);
        using var timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var periodic = WakePeriodically(timerCancellation.Token);
        try
        {
            await foreach (var _ in changes.Reader.ReadAllAsync(stoppingToken))
            {
                await Task.Delay(250, stoppingToken);
                await reconciler.ReconcileAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { await timerCancellation.CancelAsync(); await periodic; }
    }

    private async Task WakePeriodically(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try { while (await timer.WaitForNextTickAsync(token)) changes.Writer.TryWrite(true); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
