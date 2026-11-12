using ApiGateway.BFF.Metricas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.BFF.Controllers;

[ApiController]
[Route("metricas")]
public class MetricasController(MetricasProtocolo metricas) : ControllerBase
{
    /// <summary>Llamadas, errores y latencia por operación: REST (lo que entra) y gRPC (lo que sale).</summary>
    /// <remarks>Abierto para el curso. En producción se protege o se publica solo hacia el sistema de monitoreo.</remarks>
    [HttpGet("protocolos")]
    [AllowAnonymous]
    public IActionResult Protocolos() => Ok(metricas.Resumen());
}
