using System.Collections.Concurrent;

namespace ApiGateway.BFF.Metricas;

// Contadores en memoria, uno por operación y por protocolo.
// Los llenan MetricasRestMiddleware (lo que ENTRA por REST)
// y MetricasGrpcInterceptor (lo que SALE por gRPC).
public class MetricasProtocolo
{
    private readonly ConcurrentDictionary<string, Contador> _rest = new();
    private readonly ConcurrentDictionary<string, Contador> _grpc = new();

    public void Rest(string operacion, double ms, bool error) => _rest.GetOrAdd(operacion, _ => new()).Sumar(ms, error);
    public void Grpc(string metodo, double ms, bool error) => _grpc.GetOrAdd(metodo, _ => new()).Sumar(ms, error);

    public object Resumen() => new
    {
        rest = _rest.OrderBy(k => k.Key).Select(k => k.Value.Foto(k.Key)).ToList(),
        grpc = _grpc.OrderBy(k => k.Key).Select(k => k.Value.Foto(k.Key)).ToList(),
    };

    private class Contador
    {
        private long _llamadas, _errores;
        private double _totalMs, _maxMs;
        private readonly Lock _candado = new();

        public void Sumar(double ms, bool error)
        {
            lock (_candado)
            {
                _llamadas++;
                if (error) _errores++;
                _totalMs += ms;
                _maxMs = Math.Max(_maxMs, ms);
            }
        }

        public object Foto(string operacion)
        {
            lock (_candado)
                return new
                {
                    operacion,
                    llamadas = _llamadas,
                    errores = _errores,
                    promedioMs = _llamadas == 0 ? 0 : Math.Round(_totalMs / _llamadas, 1),
                    maximoMs = Math.Round(_maxMs, 1),
                };
        }
    }
}
