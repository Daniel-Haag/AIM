using System.Collections.Concurrent;
using System.Threading.Channels;
using Aim.Application;

namespace Aim.Infrastructure;

public sealed class GraphNotifications : IGraphNotifications
{
    private readonly ConcurrentDictionary<Guid, (Guid Investigation, Channel<bool> Channel)> subscriptions = new();
    public (Guid Id, ChannelReader<bool> Reader) Subscribe(Guid investigation)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        subscriptions[id] = (investigation, channel);
        channel.Writer.TryWrite(true); // A reconnect always receives the current persisted graph.
        return (id, channel.Reader);
    }
    public void Unsubscribe(Guid id) { if (subscriptions.TryRemove(id, out var item)) item.Channel.Writer.TryComplete(); }
    public void Publish(Guid investigationId)
    {
        foreach (var item in subscriptions.Values)
            if (item.Investigation == investigationId) item.Channel.Writer.TryWrite(true);
    }
}
