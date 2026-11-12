using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.BFF.Errores;

// Traducción de errores: el servicio gRPC responde con un StatusCode de gRPC
// y el cliente REST espera un código HTTP. Aquí se hace la equivalencia, una sola vez.
public class RpcExceptionHandler(ILogger<RpcExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        if (ex is not RpcException rpc) return false;

        var status = rpc.StatusCode switch
        {
            StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
            StatusCode.NotFound => StatusCodes.Status404NotFound,
            StatusCode.AlreadyExists => StatusCodes.Status409Conflict,
            StatusCode.FailedPrecondition => StatusCodes.Status409Conflict,
            StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
            StatusCode.Unauthenticated => StatusCodes.Status401Unauthorized,
            StatusCode.Unavailable => StatusCodes.Status503ServiceUnavailable,
            StatusCode.DeadlineExceeded => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError,
        };
        log.LogWarning("gRPC {Codigo} -> HTTP {Http}: {Detalle}", rpc.StatusCode, status, rpc.Status.Detail);

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = status == 503 ? "Un servicio interno no responde." : "La solicitud no se pudo completar.",
            Detail = rpc.Status.Detail,
            Extensions = { ["codigoGrpc"] = rpc.StatusCode.ToString() },
        }, ct);
        return true;
    }
}
