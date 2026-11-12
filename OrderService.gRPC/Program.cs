// OrderService.gRPC · puerto 7003 (HTTP/2) · sin base de datos: valida pedidos
// preguntándoles a UserService.gRPC (7002) y a ProductService.gRPC (7001).
using ECommerceGRPC.Products;
using ECommerceGRPC.Users;
using OrderService.gRPC.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Demo 1 · OrderService es servidor de su contrato y CLIENTE de los otros dos.
// Las direcciones salen de la configuración: en Docker son user-grpc:7002 y product-grpc:7001.
builder.Services.AddGrpcClient<UserService.UserServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcServicios:Usuarios"]!));
builder.Services.AddGrpcClient<ProductService.ProductServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcServicios:Productos"]!));

builder.Services.AddGrpc(options => options.EnableDetailedErrors = true);
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<OrderGrpcService>();
app.MapGrpcReflectionService();

Log.Information("OrderService.gRPC escuchando en el puerto 7003 (HTTP/2)");
app.Run();
