using ECommerceGRPC.Products;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProductService.gRPC.Data;

namespace ProductService.gRPC.Services;

// Implementación del contrato Protos/product.proto.
// La clase base ProductServiceBase la genera el compilador a partir del .proto.
public class ProductGrpcService(CatalogDbContext db, StockNotifier notifier, ILogger<ProductGrpcService> logger)
    : ECommerceGRPC.Products.ProductService.ProductServiceBase
{
    // ---- Unary -------------------------------------------------------------
    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "El id debe ser mayor que 0."));

        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, context.CancellationToken);

        if (product is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"No existe el producto {request.Id}."));

        return ToReply(product);
    }

    // ---- Server streaming ----------------------------------------------------
    public override async Task ListProducts(ListProductsRequest request,
        IServerStreamWriter<ProductReply> responseStream, ServerCallContext context)
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(p => p.Category == request.Category);

        await foreach (var product in query.OrderBy(p => p.Id).AsAsyncEnumerable()
                           .WithCancellation(context.CancellationToken))
        {
            await responseStream.WriteAsync(ToReply(product));
        }
    }

    // ---- Unary -------------------------------------------------------------
    public override async Task<ProductReply> CreateProduct(CreateProductRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "name y category son obligatorios."));
        if (request.Price <= 0 || request.Stock < 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "price debe ser > 0 y stock >= 0."));
        if (await db.Products.AnyAsync(p => p.Name == request.Name, context.CancellationToken))
            throw new RpcException(new Status(StatusCode.AlreadyExists, $"Ya existe un producto llamado '{request.Name}'."));

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = (decimal)request.Price,
            Stock = request.Stock,
            Category = request.Category
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Producto {Id} creado por gRPC", product.Id);
        return ToReply(product);
    }

    // ---- Unary que además avisa a WatchStock ---------------------------------
    public override async Task<ProductReply> UpdateStock(UpdateStockRequest request, ServerCallContext context)
    {
        if (request.Stock < 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "El stock no puede ser negativo."));

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"No existe el producto {request.Id}."));

        var oldStock = product.Stock;
        product.Stock = request.Stock;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);

        notifier.Publish(new StockUpdate
        {
            Id = product.Id,
            Name = product.Name,
            OldStock = oldStock,
            NewStock = product.Stock,
            ChangedAt = product.UpdatedAt.Value.ToString("O")
        });
        return ToReply(product);
    }

    // ---- Server streaming en tiempo real ---------------------------------------
    // La llamada queda abierta: cada UpdateStock del producto produce un mensaje.
    // Termina cuando el cliente cancela (Ctrl+C en grpcurl).
    public override async Task WatchStock(WatchStockRequest request,
        IServerStreamWriter<StockUpdate> responseStream, ServerCallContext context)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"No existe el producto {request.Id}."));

        var (subscriptionId, reader) = notifier.Subscribe(product.Id);
        logger.LogInformation("Cliente mirando el stock del producto {Id}", product.Id);
        try
        {
            // Primer mensaje: el stock actual, para que el cliente sepa desde dónde parte.
            await responseStream.WriteAsync(new StockUpdate
            {
                Id = product.Id,
                Name = product.Name,
                OldStock = product.Stock,
                NewStock = product.Stock,
                ChangedAt = DateTime.UtcNow.ToString("O")
            });

            await foreach (var update in reader.ReadAllAsync(context.CancellationToken))
                await responseStream.WriteAsync(update);
        }
        catch (OperationCanceledException)
        {
            // El cliente cerró la llamada: no es un error.
        }
        finally
        {
            notifier.Unsubscribe(subscriptionId);
            logger.LogInformation("Cliente dejó de mirar el producto {Id}", product.Id);
        }
    }

    private static ProductReply ToReply(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description ?? string.Empty,
        Price = (double)p.Price,
        Stock = p.Stock,
        Category = p.Category,
        IsActive = p.IsActive
    };
}
