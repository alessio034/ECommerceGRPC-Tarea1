using System.Diagnostics;

namespace ApiGateway.BFF.Metricas;

// Mide cada solicitud REST que entra al gateway: cuánto tardó y si terminó en error (4xx o 5xx).
public class MetricasRestMiddleware(RequestDelegate siguiente, MetricasProtocolo metricas)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var reloj = Stopwatch.StartNew();
        await siguiente(http);
        reloj.Stop();

        // Agrupa por plantilla de ruta: /api/v1/productos/1 y /api/v1/productos/2 cuentan juntas.
        var ruta = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText?.TrimStart('/');
        if (ruta is null || ruta.StartsWith("metricas") || ruta.StartsWith("swagger")) return;

        metricas.Rest($"{http.Request.Method} /{ruta}", reloj.Elapsed.TotalMilliseconds, http.Response.StatusCode >= 400);
    }
}
