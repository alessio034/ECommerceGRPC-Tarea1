using System.Collections.Concurrent;
using System.Threading.Channels;
using ECommerceGRPC.Products;

namespace ProductService.gRPC.Services;

// Reparte los cambios de stock a quien esté mirando (WatchStock).
// Cada suscriptor recibe su propio canal; vive en memoria del proceso.
public class StockNotifier
{
    private readonly ConcurrentDictionary<Guid, (int ProductId, Channel<StockUpdate> Channel)> _subscribers = new();

    public (Guid Id, ChannelReader<StockUpdate> Reader) Subscribe(int productId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<StockUpdate>();
        _subscribers[id] = (productId, channel);
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var sub))
            sub.Channel.Writer.TryComplete();
    }

    public void Publish(StockUpdate update)
    {
        foreach (var sub in _subscribers.Values.Where(s => s.ProductId == update.Id))
            sub.Channel.Writer.TryWrite(update);
    }
}
