using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using BC = BCrypt.Net.BCrypt;

namespace UserService.Services
{
    // CONTEXTO USUARIOS · solución completa de la Tarea 2.
    public class UserService : IUserService
    {
        private readonly UserDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(UserDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .Select(u => MapToDto(u))
                .ToListAsync();
        }

        // Un usuario desactivado "no existe" para el resto del sistema: por eso Pedidos
        // recibe 404 si intenta crear un pedido para él.
        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            return user == null ? null : MapToDto(user);
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
        {
            // Verificar si el email ya existe
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (existingUser != null)
            {
                throw new InvalidOperationException("Email already exists");
            }

            var user = new User
            {
                Email = dto.Email,
                Name = dto.Name,
                LastName = dto.LastName,
                PasswordHash = BC.HashPassword(dto.Password), // Hash del password
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created with ID {UserId}", user.Id);

            return MapToDto(user);
        }

        public async Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto dto)
        {
            // 1. Buscar el usuario
            var user = await _context.Users.FindAsync(id);
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("User {UserId} not found or inactive", id);
                return null;
            }

            // 2. Actualizar solo los campos que traen valor
            if (!string.IsNullOrEmpty(dto.Email))
            {
                // El nuevo email no puede pertenecer a otro usuario
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email == dto.Email && u.Id != id);
                if (emailExists)
                {
                    throw new InvalidOperationException("Email already exists");
                }
                user.Email = dto.Email;
            }
            if (!string.IsNullOrEmpty(dto.Name))
                user.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.LastName))
                user.LastName = dto.LastName;
            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = BC.HashPassword(dto.Password);

            user.UpdatedAt = DateTime.UtcNow;

            // 3. Guardar
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} updated successfully", id);

            return MapToDto(user);
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for deletion", id);
                return false;
            }

            // Borrado lógico: el usuario deja rastro (pedidos, auditoría); la fila se conserva
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted (soft delete)", id);
            return true;
        }

        private static UserDto MapToDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                LastName = user.LastName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
