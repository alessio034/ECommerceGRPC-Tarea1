// ProductService.gRPC · puerto 7001 (HTTP/2) · base CatalogDB (la misma de CatalogService)
using Microsoft.EntityFrameworkCore;
using ProductService.gRPC.Data;
using ProductService.gRPC.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

builder.Services.AddSingleton<StockNotifier>();

builder.Services.AddGrpc(options => options.EnableDetailedErrors = true);
// Reflection: permite que grpcurl y Postman descubran el contrato sin el .proto.
// En producción se apaga; en el curso queda encendida.
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<ProductGrpcService>();
app.MapGrpcReflectionService();

Log.Information("ProductService.gRPC escuchando en el puerto 7001 (HTTP/2)");
app.Run();
