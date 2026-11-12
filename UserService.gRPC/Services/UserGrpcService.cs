using ECommerceGRPC.Users;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using UserService.gRPC.Data;

namespace UserService.gRPC.Services;

// SOLUCIÓN de la práctica de la Clase 1.
public class UserGrpcService(UserDbContext db, UserEventNotifier notifier, ILogger<UserGrpcService> logger)
    : ECommerceGRPC.Users.UserService.UserServiceBase
{
    // TODO 2 · GetUser (unary)
    public override async Task<UserReply> GetUser(GetUserRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "El id debe ser mayor que 0."));

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.Id, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"No existe el usuario {request.Id}."));

        return ToReply(user);
    }

    // TODO 3 · CreateUser (unary)
    public override async Task<UserReply> CreateUser(CreateUserRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.LastName) || request.Password.Length < 8)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "email, name y last_name son obligatorios; password debe tener al menos 8 caracteres."));

        if (await db.Users.AnyAsync(u => u.Email == request.Email, context.CancellationToken))
            throw new RpcException(new Status(StatusCode.AlreadyExists, $"El correo {request.Email} ya está registrado."));

        var user = new User
        {
            Email = request.Email,
            Name = request.Name,
            LastName = request.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Usuario {Id} creado por gRPC", user.Id);

        var reply = ToReply(user);
        notifier.Publish(NewEvent("CREATED", reply));   // TODO 6 · aviso a WatchUserEvents
        return reply;
    }

    // TODO 4 · ListUsers (server streaming)
    public override async Task ListUsers(ListUsersRequest request,
        IServerStreamWriter<UserReply> responseStream, ServerCallContext context)
    {
        await foreach (var user in db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Id)
                           .AsAsyncEnumerable().WithCancellation(context.CancellationToken))
        {
            await responseStream.WriteAsync(ToReply(user));
        }
    }

    // TODO 5 · DeactivateUser (unary)
    public override async Task<DeactivateUserReply> DeactivateUser(DeactivateUserRequest request, ServerCallContext context)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"No existe el usuario {request.Id}."));

        if (!user.IsActive)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, $"El usuario {request.Id} ya estaba desactivado."));

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);

        notifier.Publish(NewEvent("DEACTIVATED", ToReply(user)));   // TODO 6 · aviso a WatchUserEvents
        return new DeactivateUserReply { Success = true, Message = $"Usuario {user.Id} desactivado." };
    }

    // TODO 6 · WatchUserEvents (server streaming en tiempo real)
    public override async Task WatchUserEvents(WatchUserEventsRequest request,
        IServerStreamWriter<UserEvent> responseStream, ServerCallContext context)
    {
        var (subscriptionId, reader) = notifier.Subscribe();
        try
        {
            await foreach (var userEvent in reader.ReadAllAsync(context.CancellationToken))
                await responseStream.WriteAsync(userEvent);
        }
        catch (OperationCanceledException)
        {
            // El cliente cerró la llamada: no es un error.
        }
        finally
        {
            notifier.Unsubscribe(subscriptionId);
        }
    }

    // Ya viene escrito: convierte la entidad en el mensaje del contrato.
    private static UserReply ToReply(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        Name = u.Name,
        LastName = u.LastName,
        IsActive = u.IsActive,
        CreatedAt = DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc).ToString("O")   // la base guarda UTC sin la marca
    };

    private static UserEvent NewEvent(string type, UserReply user) => new()
    {
        Type = type,
        User = user,
        OccurredAt = DateTime.UtcNow.ToString("O")
    };
}
