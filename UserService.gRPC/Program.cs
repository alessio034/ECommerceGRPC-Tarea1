// UserService.gRPC · puerto 7002 (HTTP/2) · base UserDB (la misma de UserService REST)
using Microsoft.EntityFrameworkCore;
using Serilog;
using UserService.gRPC.Data;
using UserService.gRPC.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

builder.Services.AddSingleton<UserEventNotifier>();

builder.Services.AddGrpc(options => options.EnableDetailedErrors = true);
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<UserGrpcService>();
app.MapGrpcReflectionService();

Log.Information("UserService.gRPC escuchando en el puerto 7002 (HTTP/2)");
app.Run();
