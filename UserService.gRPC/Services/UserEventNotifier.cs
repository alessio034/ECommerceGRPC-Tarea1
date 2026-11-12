using System.Collections.Concurrent;
using System.Threading.Channels;
using ECommerceGRPC.Users;

namespace UserService.gRPC.Services;

// Ya viene escrito. Es el mismo mecanismo que StockNotifier en ProductService.gRPC:
// cada cliente de WatchUserEvents recibe su propio canal en memoria.
public class UserEventNotifier
{
    private readonly ConcurrentDictionary<Guid, Channel<UserEvent>> _subscribers = new();

    public (Guid Id, ChannelReader<UserEvent> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<UserEvent>();
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    public void Publish(UserEvent userEvent)
    {
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(userEvent);
    }
}
