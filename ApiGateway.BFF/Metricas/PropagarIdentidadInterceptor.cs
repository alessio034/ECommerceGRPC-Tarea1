using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ApiGateway.BFF.Metricas;

// Propagación de identidad: el gateway ya validó el JWT; a los servicios de adentro
// les pasa QUIÉN pide, en los metadatos gRPC (encabezado x-usuario), sin reenviar el token.
public class PropagarIdentidadInterceptor(IHttpContextAccessor accesor) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        => continuation(request, ConIdentidad(context));

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        => continuation(request, ConIdentidad(context));

    private ClientInterceptorContext<TReq, TResp> ConIdentidad<TReq, TResp>(ClientInterceptorContext<TReq, TResp> ctx)
        where TReq : class where TResp : class
    {
        var email = accesor.HttpContext?.User.Identity?.Name;
        if (email is null) return ctx;

        var metadatos = ctx.Options.Headers ?? new Metadata();
        metadatos.Add("x-usuario", email);
        return new ClientInterceptorContext<TReq, TResp>(ctx.Method, ctx.Host, ctx.Options.WithHeaders(metadatos));
    }
}
