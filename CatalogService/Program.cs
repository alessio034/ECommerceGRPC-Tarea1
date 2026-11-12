// ============================================================
// CatalogService/Program.cs · CONTEXTO CATÁLOGO · puerto 5001 · base CatalogDB
//
// Semana 3: Catálogo no cambia de responsabilidad. Solo RESPONDE cuando
// Pedidos le pregunta por un producto (GET /api/products/{id}).
// ============================================================

using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog · Semana 5: la consola de siempre y, si hay dirección, Seq (el visor de logs).
// La dirección llega por configuración: «Seq:Url» en appsettings.json, o «Seq__Url» en el compose (S5-03).
// Sin dirección, el servicio funciona igual: el visor de logs no es una dependencia.
var seqUrl = builder.Configuration["Seq:Url"];
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    // S5-10 · enriquecedor: cada línea dice de qué servicio viene (en Seq: Servicio = 'catalog-service')
    .Enrich.WithProperty("Servicio", "catalog-service");
if (!string.IsNullOrWhiteSpace(seqUrl))
    logConfig.WriteTo.Seq(seqUrl);
Log.Logger = logConfig.CreateLogger();

// Usar Serilog
builder.Host.UseSerilog();

// Agregar servicios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Catalog Service API",
        Version = "v1",
        Description = "Microservicio de Catálogo de Productos",
        Contact = new OpenApiContact
        {
            Name = "Equipo de ECommerce Monolítico",
            Email = "soporte@ECommerceMonolítico.com"
        }
    });
});

// ---- Base de datos: selector de proveedor (S3-13) ----------------------------
// "Database:Provider" en appsettings.json decide el motor: SqlServer (por omisión) o PostgreSql.
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString(provider)
    ?? throw new InvalidOperationException($"Falta ConnectionStrings:{provider} en appsettings.json");

builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    if (provider == "PostgreSql")
        options.UseNpgsql(connectionString);
    else
        options.UseSqlServer(connectionString);
});

// Registrar servicios y validadores
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// S3-11 (extensión opcional de la Semana 3; sigue sin activar) [Catálogo ⇠ evento]
// Catálogo también puede ESCUCHAR "PedidoCreado" y descontar el stock (consistencia eventual).
// El consumidor está listo en Consumers/OrderCreatedConsumer.cs; falta registrarlo aquí:
//
// builder.Services.AddMassTransit(x =>
// {
//     x.AddConsumer<CatalogService.Consumers.OrderCreatedConsumer>();
//     x.UsingRabbitMq((context, cfg) =>
//     {
//         cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
//         {
//             h.Username(builder.Configuration["RabbitMQ:Username"] ?? "admin");
//             h.Password(builder.Configuration["RabbitMQ:Password"] ?? "admin123");
//         });
//         // Cola PROPIA de Catálogo: cada contexto recibe su propia copia del evento
//         cfg.ReceiveEndpoint("order-created-catalog", e =>
//         {
//             e.ConfigureConsumer<CatalogService.Consumers.OrderCreatedConsumer>(context);
//         });
//     });
// });

// Health checks (según el proveedor)
var healthChecks = builder.Services.AddHealthChecks();
if (provider == "PostgreSql")
    healthChecks.AddNpgSql(connectionString, name: "database");
else
    healthChecks.AddSqlServer(connectionString, name: "database", tags: new[] { "db", "sql", "sqlserver" });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configurar el pipeline
// S5-05 · [Catálogo · logs] CorrelationId: el número de expediente de la petición.
// Si la petición ya trae el header X-Correlation-Id (porque otro servicio lo mandó), se usa ese;
// si no, se crea uno nuevo. Se guarda en context.Items (lo lee el HttpClient, S5-05), se devuelve
// en la respuesta y se mete en el LogContext: desde aquí, CADA línea de log de esta petición
// lo lleva como propiedad. Va ANTES de UseSerilogRequestLogging, para que esa línea también lo lleve.
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N")[..12];
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
app.UseSerilogRequestLogging();
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog Service API v1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz
    });
}

// Crear la base al arrancar: migraciones en SQL Server; esquema desde el modelo en PostgreSQL.
// Semana 4: en contenedores, SQL Server puede tardar 20 a 40 segundos en aceptar conexiones.
// «Arrancó» no es «está listo» (S4-07): por eso se reintenta cada 3 s, hasta 40 veces (unos 2 minutos), antes de rendirse.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    const int maxIntentos = 40;
    for (var intento = 1; intento <= maxIntentos; intento++)
    {
        try
        {
            if (provider == "PostgreSql")
            {
                dbContext.Database.EnsureCreated();
                Log.Information("Database created (PostgreSQL, EnsureCreated)");
            }
            else
            {
                dbContext.Database.Migrate();
                Log.Information("Database migrated successfully");
            }
            break;
        }
        catch (Exception ex) when (intento < maxIntentos)
        {
            Log.Warning("La base todavía no responde (intento {Intento} de {Max}): {Motivo}. Reintento en 3 s",
                intento, maxIntentos, ex.GetBaseException().Message);
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error preparing database");
        }
    }
}

// ============ ENDPOINTS ============

// GET /api/products - Obtener todos los productos
app.MapGet("/api/products", async (IProductService productService) =>
{
    try
    {
        var products = await productService.GetAllProductsAsync();
        return Results.Ok(ApiResponse<List<ProductDto>>.Ok(products, "Products retrieved successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting products");
        return Results.Problem("An error occurred while fetching products");
    }
})
.WithName("GetProducts")
.WithOpenApi()
.Produces<ApiResponse<List<ProductDto>>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);

// GET /api/products/{id} - Obtener producto por ID
// (Es el endpoint que Pedidos consulta en la Semana 3: "¿cuánto vale el producto {id}?")
app.MapGet("/api/products/{id:int}", async (int id, IProductService productService) =>
{
    try
    {
        var product = await productService.GetProductByIdAsync(id);
        if (product == null)
            return Results.NotFound(ApiResponse<ProductDto>.Fail($"Product with ID {id} not found"));

        return Results.Ok(ApiResponse<ProductDto>.Ok(product));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting product {ProductId}", id);
        return Results.Problem($"An error occurred while fetching product {id}");
    }
})
.WithName("GetProductById")
.WithOpenApi()
.Produces<ApiResponse<ProductDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// POST /api/products - Crear nuevo producto (con FluentValidation, Demo 2 de la Semana 2)
app.MapPost("/api/products", async (
    CreateProductDto dto,
    IProductService productService,
    IValidator<CreateProductDto> validator) =>
{
    var validation = await validator.ValidateAsync(dto);
    if (!validation.IsValid)
    {
        return Results.BadRequest(ApiResponse<ProductDto>.Fail(
            "Validation failed",
            validation.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        var product = await productService.CreateProductAsync(dto);
        return Results.Created($"/api/products/{product.Id}",
            ApiResponse<ProductDto>.Ok(product, "Product created successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creating product");
        return Results.Problem("An error occurred while creating the product");
    }
})
.WithName("CreateProduct")
.WithOpenApi()
.Produces<ApiResponse<ProductDto>>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);

// PUT /api/products/{id} - Actualizar producto
app.MapPut("/api/products/{id:int}", async (int id, UpdateProductDto dto, IProductService productService) =>
{
    try
    {
        var product = await productService.UpdateProductAsync(id, dto);
        if (product == null)
            return Results.NotFound(ApiResponse<ProductDto>.Fail($"Product with ID {id} not found"));

        return Results.Ok(ApiResponse<ProductDto>.Ok(product, "Product updated successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error updating product {ProductId}", id);
        return Results.Problem($"An error occurred while updating product {id}");
    }
})
.WithName("UpdateProduct")
.WithOpenApi()
.Produces<ApiResponse<ProductDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// DELETE /api/products/{id} - Eliminar producto
app.MapDelete("/api/products/{id:int}", async (int id, IProductService productService) =>
{
    try
    {
        var result = await productService.DeleteProductAsync(id);
        if (!result)
            return Results.NotFound(ApiResponse<bool>.Fail($"Product with ID {id} not found"));

        return Results.Ok(ApiResponse<bool>.Ok(true, "Product deleted successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error deleting product {ProductId}", id);
        return Results.Problem($"An error occurred while deleting product {id}");
    }
})
.WithName("DeleteProduct")
.WithOpenApi()
.Produces<ApiResponse<bool>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// GET /api/products/search?term={term} - Buscar productos
app.MapGet("/api/products/search", async (string term, IProductService productService) =>
{
    try
    {
        var products = await productService.SearchProductsAsync(term);
        return Results.Ok(ApiResponse<List<ProductDto>>.Ok(products, $"Found {products.Count} products"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error searching products");
        return Results.Problem("An error occurred while searching products");
    }
})
.WithName("SearchProducts")
.WithOpenApi()
.Produces<ApiResponse<List<ProductDto>>>(StatusCodes.Status200OK);

// Health check endpoint
app.MapHealthChecks("/health");

// Endpoint de información
app.MapGet("/info", () => new
{
    Service = "Catalog Service",
    Context = "Catálogo",
    Version = "1.0.0",
    Status = "Running",
    Database = provider,
    Endpoints = new[]
    {
        "GET /api/products",
        "GET /api/products/{id}",
        "POST /api/products",
        "PUT /api/products/{id}",
        "DELETE /api/products/{id}",
        "GET /api/products/search?term={term}",
        "GET /health"
    },
    Documentation = "/ (Swagger UI)"
});

Console.WriteLine("Microservicio de Catálogo de Productos iniciado");
Console.WriteLine("Swagger disponible en: http://localhost:5001 (o https://localhost:7001)");
app.Run();

// Hacer la clase Program testeable
public partial class Program { }
