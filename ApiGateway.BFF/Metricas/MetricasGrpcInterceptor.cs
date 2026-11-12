using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ApiGateway.BFF.Metricas;

// Mide cada llamada gRPC que SALE del gateway hacia Productos, Usuarios o Pedidos.
public class MetricasGrpcInterceptor(MetricasProtocolo metricas) : Interceptor
{
    // Unary: una petición, una respuesta. Se mide hasta que llega la respuesta.
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var reloj = Stopwatch.StartNew();
        var llamada = continuation(request, context);
        return new AsyncUnaryCall<TResponse>(Medir(llamada.ResponseAsync, context.Method.FullName, reloj),
            llamada.ResponseHeadersAsync, llamada.GetStatus, llamada.GetTrailers, llamada.Dispose);
    }

    // Server streaming: se mide hasta que el stream se termina de leer.
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var reloj = Stopwatch.StartNew();
        var llamada = continuation(request, context);
        var lector = new LectorMedido<TResponse>(llamada.ResponseStream, metricas, context.Method.FullName, reloj);
        return new AsyncServerStreamingCall<TResponse>(lector,
            llamada.ResponseHeadersAsync, llamada.GetStatus, llamada.GetTrailers, llamada.Dispose);
    }

    private async Task<T> Medir<T>(Task<T> respuesta, string metodo, Stopwatch reloj)
    {
        try
        {
            var r = await respuesta;
            metricas.Grpc(metodo, reloj.Elapsed.TotalMilliseconds, error: false);
            return r;
        }
        catch (RpcException)
        {
            metricas.Grpc(metodo, reloj.Elapsed.TotalMilliseconds, error: true);
            throw;
        }
    }

    private class LectorMedido<T>(IAsyncStreamReader<T> real, MetricasProtocolo m, string metodo, Stopwatch reloj)
        : IAsyncStreamReader<T>
    {
        public T Current => real.Current;

        public async Task<bool> MoveNext(CancellationToken ct)
        {
            try
            {
                var hay = await real.MoveNext(ct);
                if (!hay) m.Grpc(metodo, reloj.Elapsed.TotalMilliseconds, error: false);
                return hay;
            }
            catch (RpcException)
            {
                m.Grpc(metodo, reloj.Elapsed.TotalMilliseconds, error: true);
                throw;
            }
        }
    }
}
