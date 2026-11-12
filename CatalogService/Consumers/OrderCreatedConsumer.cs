using CatalogService.Data;
using ECommerce.Contracts.Events;
using MassTransit;

namespace CatalogService.Consumers
{
    // ============================================================
    // S3-11 (extensión opcional de la Semana 3; sigue sin activar) [Catálogo ⇠ evento · consistencia eventual]
    //
    // Cuando Pedidos avisa "PedidoCreado", Catálogo descuenta el stock de cada
    // artículo. Nadie le pidió a Catálogo que lo hiciera: Catálogo DECIDIÓ
    // escuchar ese evento. Pedidos ni se entera.
    //
    // Consistencia eventual: durante unos milisegundos el pedido existe y el
    // stock todavía no bajó. Para un e-commerce es aceptable; para un banco, no.
    //
    // Para activarlo: descomentar el bloque AddMassTransit en Program.cs.
    // ============================================================
    public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<OrderCreatedConsumer> _logger;

        public OrderCreatedConsumer(CatalogDbContext context, ILogger<OrderCreatedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
        {
            var e = context.Message;
            _logger.LogInformation("[Catálogo ⇠ evento] Pedido {OrderId} recibido: descontando stock de {Count} artículo(s)", e.OrderId, e.Items.Count);

            foreach (var item in e.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("[Catálogo ⇠ evento] Producto {ProductId} no existe; se ignora", item.ProductId);
                    continue;
                }

                product.Stock = Math.Max(0, product.Stock - item.Quantity);
                product.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("[Catálogo ⇠ evento] {ProductName}: stock → {Stock}", product.Name, product.Stock);
            }

            await _context.SaveChangesAsync();
        }
    }
}
