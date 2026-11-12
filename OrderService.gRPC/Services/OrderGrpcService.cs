using ECommerceGRPC.Orders;
using ECommerceGRPC.Products;
using ECommerceGRPC.Users;
using Grpc.Core;

namespace OrderService.gRPC.Services;

// Valida un pedido llamando a otros dos servicios gRPC. Es el primer servicio
// del curso que es servidor y cliente gRPC a la vez.
public class OrderGrpcService(
    UserService.UserServiceClient usuarios,
    ProductService.ProductServiceClient productos,
    ILogger<OrderGrpcService> log) : ECommerceGRPC.Orders.OrderService.OrderServiceBase
{
    public override async Task<ValidateOrderReply> ValidateOrder(ValidateOrderRequest request, ServerCallContext context)
    {
        // Identidad propagada por el gateway en los metadatos (encabezado x-usuario).
        var quien = context.RequestHeaders.GetValue("x-usuario") ?? "(sin identidad)";
        log.LogInformation("ValidateOrder pedido por {Quien}: usuario {UserId}, {N} líneas",
            quien, request.UserId, request.Items.Count);

        if (request.Items.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "El pedido no tiene productos."));

        var reply = new ValidateOrderReply();

        // Demo 1 · 1.ª llamada: ¿existe el usuario y está activo? (UserService, puerto 7002)
        try
        {
            var usuario = await usuarios.GetUserAsync(new GetUserRequest { Id = request.UserId },
                cancellationToken: context.CancellationToken);
            if (!usuario.IsActive)
                reply.Problems.Add($"El usuario {request.UserId} está desactivado.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            reply.Problems.Add($"No existe el usuario {request.UserId}.");
        }

        // Demo 1 · 2.ª llamada, una por línea: ¿existe el producto y alcanza el stock? (ProductService, puerto 7001)
        foreach (var item in request.Items)
        {
            try
            {
                var p = await productos.GetProductAsync(new GetProductRequest { Id = item.ProductId },
                    cancellationToken: context.CancellationToken);
                if (item.Quantity <= 0)
                    reply.Problems.Add($"La cantidad de {p.Name} debe ser mayor que 0.");
                else if (p.Stock < item.Quantity)
                    reply.Problems.Add($"{p.Name}: pidió {item.Quantity} y hay {p.Stock}.");
                else
                    reply.Total += p.Price * item.Quantity;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                reply.Problems.Add($"No existe el producto {item.ProductId}.");
            }
        }

        reply.IsValid = reply.Problems.Count == 0;
        reply.Total = Math.Round(reply.Total, 2);
        return reply;
    }
}
