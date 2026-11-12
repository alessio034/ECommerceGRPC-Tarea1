// Benchmark · compara REST (CatalogService, puerto 5001) contra gRPC (ProductService.gRPC, puerto 7001)
// pidiendo el MISMO producto de la MISMA base CatalogDB.
//
// Uso:  dotnet run -c Release -- [peticiones] [concurrencia] [idProducto]
//       dotnet run -c Release -- 500 16 1
using System.Diagnostics;
using ECommerceGRPC.Products;
using Grpc.Net.Client;

int total = args.Length > 0 ? int.Parse(args[0]) : 500;
int concurrency = args.Length > 1 ? int.Parse(args[1]) : 16;
int productId = args.Length > 2 ? int.Parse(args[2]) : 1;

var restUrl = $"http://localhost:5001/api/products/{productId}";
using var http = new HttpClient();
using var channel = GrpcChannel.ForAddress("http://localhost:7001");
var grpc = new ProductService.ProductServiceClient(channel);

// Tamaño de lo que viaja por la red (solo el cuerpo de la respuesta)
var restBytes = (await http.GetByteArrayAsync(restUrl)).Length;
var grpcReply = await grpc.GetProductAsync(new GetProductRequest { Id = productId });
var grpcBytes = grpcReply.CalculateSize();

Console.WriteLine($"Producto {productId}: {grpcReply.Name}");
Console.WriteLine($"Peticiones por protocolo: {total} · concurrencia: {concurrency}");
Console.WriteLine("Calentando (50 llamadas de cada uno)...");
for (int i = 0; i < 50; i++)
{
    await http.GetStringAsync(restUrl);
    await grpc.GetProductAsync(new GetProductRequest { Id = productId });
}

var rest = await Measure(() => http.GetStringAsync(restUrl));
var rpc = await Measure(async () => await grpc.GetProductAsync(new GetProductRequest { Id = productId }));

Console.WriteLine();
Console.WriteLine("| Medida | REST (JSON, HTTP/1.1) | gRPC (Protobuf, HTTP/2) |");
Console.WriteLine("|---|---|---|");
Console.WriteLine($"| Latencia mediana (p50), ms | {rest.P50:F2} | {rpc.P50:F2} |");
Console.WriteLine($"| Latencia p95, ms | {rest.P95:F2} | {rpc.P95:F2} |");
Console.WriteLine($"| Rendimiento, peticiones/s | {rest.Rps:F0} | {rpc.Rps:F0} |");
Console.WriteLine($"| Tamaño de la respuesta, bytes | {restBytes} | {grpcBytes} |");
Console.WriteLine();
Console.WriteLine($"gRPC/REST: latencia p50 {rest.P50 / rpc.P50:F1} veces menor · rendimiento {rpc.Rps / rest.Rps:F1} veces mayor");

async Task<(double P50, double P95, double Rps)> Measure(Func<Task> call)
{
    var latencies = new double[total];
    int next = -1;
    var clock = Stopwatch.StartNew();
    var workers = Enumerable.Range(0, concurrency).Select(async _ =>
    {
        int i;
        while ((i = Interlocked.Increment(ref next)) < total)
        {
            var sw = Stopwatch.StartNew();
            await call();
            latencies[i] = sw.Elapsed.TotalMilliseconds;
        }
    });
    await Task.WhenAll(workers);
    clock.Stop();
    Array.Sort(latencies);
    return (latencies[total / 2], latencies[(int)(total * 0.95)], total / clock.Elapsed.TotalSeconds);
}
