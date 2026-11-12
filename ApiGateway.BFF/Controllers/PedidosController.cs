using ApiGateway.BFF.Modelos;
using ECommerceGRPC.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApiGateway.BFF.Controllers;

// Tercer servicio gRPC: OrderService.gRPC (7003), que a su vez llama a Usuarios y a Productos.
[ApiController]
[Route("api/v1/pedidos")]
[Authorize]
public class PedidosController(OrderService.OrderServiceClient pedidos) : ControllerBase
{
    /// <summary>Valida un pedido del usuario que inició sesión (no lo guarda).</summary>
    /// <remarks>El id del usuario no va en el JSON: el gateway lo toma del token (claim "sub").</remarks>
    [HttpPost("validar")]
    [EnableRateLimiting("lecturas")]
    [ProducesResponseType<ValidacionPedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ValidacionPedidoDto>> Validar(PedidoDto dto, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var userId))
            return Unauthorized(new { error = "Inicie sesión: el pedido sale del usuario del token." });

        var reply = await pedidos.ValidateOrderAsync(Mapeos.AValidateOrder(userId, dto), cancellationToken: ct);
        return Ok(Mapeos.ADto(reply));
    }
}
