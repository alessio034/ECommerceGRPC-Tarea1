// ============================================================
// UserService/Program.cs · CONTEXTO USUARIOS · puerto 5002 · base UserDB
//
// Semana 3: Usuarios ya está completo (es la solución de la Tarea 2).
// Su papel esta semana es RESPONDER cuando Pedidos pregunta
// "¿existe el usuario {id}?" con GET /api/users/{id}: 200 si existe y está
// activo, 404 si no. Nada más cambia.
// ============================================================

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using UserService.Data;
using UserService.DTOs;
using UserService.Services;

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
    // S5-10 · enriquecedor: cada línea dice de qué servicio viene (en Seq: Servicio = 'user-service')
    .Enrich.WithProperty("Servicio", "user-service");
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
        Title = "User Service API",
        Version = "v1",
        Description = "Microservicio de Gestión de Usuarios"
    });
});

// ---- Base de datos: selector de proveedor (S3-13) ----------------------------
// "Database:Provider" en appsettings.json decide el motor: SqlServer (por omisión) o PostgreSql.
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString(provider)
    ?? throw new InvalidOperationException($"Falta ConnectionStrings:{provider} en appsettings.json");

builder.Services.AddDbContext<UserDbContext>(options =>
{
    if (provider == "PostgreSql")
        options.UseNpgsql(connectionString);
    else
        options.UseSqlServer(connectionString);
});

// Registrar servicios y validadores (Tarea 2)
builder.Services.AddScoped<IUserService, UserService.Services.UserService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Health checks (según el proveedor)
var healthChecks = builder.Services.AddHealthChecks();
if (provider == "PostgreSql")
    healthChecks.AddNpgSql(connectionString, name: "database");
else
    healthChecks.AddSqlServer(connectionString, name: "database");

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
// S5-05 · [Usuarios · logs] CorrelationId: el número de expediente de la petición.
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Service API v1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz
    });
}

// Crear la base al arrancar: migraciones en SQL Server; esquema desde el modelo en PostgreSQL.
// Semana 4: en contenedores, SQL Server puede tardar 20 a 40 segundos en aceptar conexiones.
// «Arrancó» no es «está listo» (S4-07): por eso se reintenta cada 3 s, hasta 40 veces (unos 2 minutos), antes de rendirse.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
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

// ============================================
// ENDPOINTS
// ============================================

// GET /api/users - Obtener todos los usuarios
app.MapGet("/api/users", async (IUserService userService) =>
{
    try
    {
        Log.Information("Getting all users");
        var users = await userService.GetAllUsersAsync();
        Log.Information("Retrieved {Count} users", users.Count);
        return Results.Ok(ApiResponse<List<UserDto>>.Ok(users, "Users retrieved successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting users");
        return Results.Problem("An error occurred while fetching users");
    }
})
.WithName("GetUsers")
.WithOpenApi()
.Produces<ApiResponse<List<UserDto>>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);

// GET /api/users/{id} - Obtener usuario por ID
// (Es el endpoint que Pedidos consulta en la Semana 3: "¿existe el usuario {id}?")
app.MapGet("/api/users/{id:int}", async (int id, IUserService userService) =>
{
    try
    {
        Log.Information("Getting user with ID {UserId}", id);
        var user = await userService.GetUserByIdAsync(id);

        if (user == null)
        {
            Log.Warning("User with ID {UserId} not found", id);
            return Results.NotFound(ApiResponse<UserDto>.Fail($"User with ID {id} not found"));
        }

        Log.Information("User {UserId} retrieved successfully", id);
        return Results.Ok(ApiResponse<UserDto>.Ok(user));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting user {UserId}", id);
        return Results.Problem($"An error occurred while fetching user {id}");
    }
})
.WithName("GetUserById")
.WithOpenApi()
.Produces<ApiResponse<UserDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// POST /api/users - Crear nuevo usuario
app.MapPost("/api/users", async (
    CreateUserDto dto,
    IUserService userService,
    IValidator<CreateUserDto> validator) =>
{
    // Validar la forma del dato (Tarea 2, B.3)
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(ApiResponse<UserDto>.Fail(
            "Validation failed",
            validationResult.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        Log.Information("Creating new user with email {Email}", dto.Email);
        var user = await userService.CreateUserAsync(dto);

        Log.Information("User created with ID {UserId}", user.Id);
        return Results.Created($"/api/users/{user.Id}",
            ApiResponse<UserDto>.Ok(user, "User created successfully"));
    }
    catch (InvalidOperationException ex)
    {
        // El email ya existe: es un error del cliente (400), no del servidor (500)
        Log.Warning(ex, "Validation error creating user");
        return Results.BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creating user");
        return Results.Problem("An error occurred while creating the user");
    }
})
.WithName("CreateUser")
.WithOpenApi()
.Produces<ApiResponse<UserDto>>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);

// PUT /api/users/{id} - Actualizar usuario (Tarea 2, B.1)
app.MapPut("/api/users/{id:int}", async (
    int id,
    UpdateUserDto dto,
    IUserService userService,
    IValidator<UpdateUserDto> validator) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(ApiResponse<UserDto>.Fail(
            "Validation failed",
            validationResult.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        Log.Information("Updating user {UserId}", id);
        var user = await userService.UpdateUserAsync(id, dto);
        if (user == null)
        {
            Log.Warning("User {UserId} not found for update", id);
            return Results.NotFound(ApiResponse<UserDto>.Fail($"User with ID {id} not found"));
        }
        Log.Information("User {UserId} updated successfully", id);
        return Results.Ok(ApiResponse<UserDto>.Ok(user, "User updated successfully"));
    }
    catch (InvalidOperationException ex)
    {
        Log.Warning(ex, "Validation error updating user {UserId}", id);
        return Results.BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error updating user {UserId}", id);
        return Results.Problem($"An error occurred while updating user {id}");
    }
})
.WithName("UpdateUser")
.WithOpenApi()
.Produces<ApiResponse<UserDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status400BadRequest);

// DELETE /api/users/{id} - Eliminar usuario (borrado lógico, Tarea 2, B.2)
app.MapDelete("/api/users/{id:int}", async (int id, IUserService userService) =>
{
    try
    {
        Log.Information("Deleting user {UserId}", id);
        var result = await userService.DeleteUserAsync(id);
        if (!result)
        {
            Log.Warning("User {UserId} not found for deletion", id);
            return Results.NotFound(ApiResponse<bool>.Fail($"User with ID {id} not found"));
        }
        Log.Information("User {UserId} deleted successfully", id);
        return Results.Ok(ApiResponse<bool>.Ok(true, "User deleted successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error deleting user {UserId}", id);
        return Results.Problem($"An error occurred while deleting user {id}");
    }
})
.WithName("DeleteUser")
.WithOpenApi()
.Produces<ApiResponse<bool>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Health check endpoint
app.MapHealthChecks("/health");

// Endpoint de información
app.MapGet("/info", () => new
{
    Service = "User Service",
    Context = "Usuarios",
    Version = "1.0.0",
    Status = "Running",
    Database = provider,
    Endpoints = new[]
    {
        "GET /api/users",
        "GET /api/users/{id}",
        "POST /api/users",
        "PUT /api/users/{id}",
        "DELETE /api/users/{id}",
        "GET /health"
    },
    Documentation = "/ (Swagger UI)"
});

Console.WriteLine("Microservicio de Usuarios iniciado");
Console.WriteLine("Swagger disponible en: http://localhost:5002 (o https://localhost:7002)");
app.Run();

// Hacer la clase Program testeable
public partial class Program { }
