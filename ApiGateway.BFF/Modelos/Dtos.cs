using System.ComponentModel.DataAnnotations;

namespace ApiGateway.BFF.Modelos;

// Lo que ve el cliente REST. No es el mensaje gRPC: el gateway transforma uno en otro (Mapeos.cs).

public record LoginDto(string Email, string Clave);

public record TokenDto(string Token, DateTime ExpiraEn, string Rol);

public class ProductoDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = "";
    public string? Descripcion { get; init; }
    public decimal? Precio { get; init; }
    public string? Categoria { get; init; }
    public int? Stock { get; init; }
    public bool? Disponible { get; init; }
}

public record ActualizarStockDto(int Cantidad);

/// <summary>Datos para crear un producto (POST /api/v1/productos).</summary>
public record CrearProductoDto(
    [Required, StringLength(100)] string Nombre,
    [StringLength(500)] string? Descripcion,
    [Range(typeof(decimal), "0.01", "999999.99")] decimal Precio,
    [Range(0, int.MaxValue)] int Stock,
    [Required, StringLength(50)] string Categoria);

/// <summary>Datos para registrarse (POST /api/v1/usuarios/registro).</summary>
public record RegistroDto(
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, StringLength(50)] string Nombre,
    [Required, StringLength(50)] string Apellido,
    [Required, MinLength(8)] string Clave);

public record UsuarioDto(int Id, string Email, string NombreCompleto, bool Activo, DateTime CreadoEl);

public record LineaPedidoDto(int ProductoId, int Cantidad);

public record PedidoDto(List<LineaPedidoDto> Lineas);

public record ValidacionPedidoDto(bool Valido, List<string> Problemas, decimal Total);
