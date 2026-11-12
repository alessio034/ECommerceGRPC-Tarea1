using System.Globalization;
using ECommerceGRPC.Orders;
using ECommerceGRPC.Products;
using ECommerceGRPC.Users;

namespace ApiGateway.BFF.Modelos;

// Transformación de datos en el gateway: mensaje gRPC ⇄ JSON del cliente REST.
public static class Mapeos
{
    // Respuesta: ProductReply (gRPC) → ProductoDto (REST).
    public static ProductoDto ADto(ProductReply p) => new()
    {
        Id = p.Id,
        Nombre = p.Name,
        Descripcion = p.Description,
        Precio = Math.Round((decimal)p.Price, 2),
        Categoria = p.Category,
        Stock = p.Stock,
        Disponible = p.IsActive && p.Stock > 0,
    };

    // Solicitud: ActualizarStockDto (REST) → UpdateStockRequest (gRPC). El id viene de la ruta.
    public static UpdateStockRequest AUpdateStock(int id, ActualizarStockDto dto)
    {
        return new UpdateStockRequest { Id = id, Stock = dto.Cantidad };
    }

    // Solicitud: CrearProductoDto (REST) → CreateProductRequest (gRPC). proto3 no tiene decimal: viaja como double.
    public static CreateProductRequest ACreateProduct(CrearProductoDto dto) => new()
    {
        Name = dto.Nombre.Trim(),
        Description = dto.Descripcion?.Trim() ?? string.Empty,
        Price = (double)dto.Precio,
        Stock = dto.Stock,
        Category = dto.Categoria.Trim(),
    };

    // Solicitud: RegistroDto (REST) → CreateUserRequest (gRPC). La clave viaja tal cual: el hash lo hace UserService.gRPC.
    public static CreateUserRequest ACreateUser(RegistroDto dto) => new()
    {
        Email = dto.Email.Trim().ToLowerInvariant(),
        Name = dto.Nombre.Trim(),
        LastName = dto.Apellido.Trim(),
        Password = dto.Clave,
    };

    // UserReply → UsuarioDto: une nombre y apellido, y convierte el texto ISO 8601 en fecha.
    public static UsuarioDto ADto(UserReply u) => new(
        u.Id,
        u.Email,
        $"{u.Name} {u.LastName}".Trim(),
        u.IsActive,
        DateTime.Parse(u.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));

    // PedidoDto + id del token → ValidateOrderRequest. El usuario NO viene en el JSON: sale del JWT.
    public static ValidateOrderRequest AValidateOrder(int userId, PedidoDto dto)
    {
        var req = new ValidateOrderRequest { UserId = userId };
        req.Items.AddRange(dto.Lineas.Select(l => new OrderItem { ProductId = l.ProductoId, Quantity = l.Cantidad }));
        return req;
    }

    public static ValidacionPedidoDto ADto(ValidateOrderReply r) =>
        new(r.IsValid, r.Problems.ToList(), Math.Round((decimal)r.Total, 2));
}
