using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data
{
    public class UserDbContext : DbContext
    {
        public UserDbContext(DbContextOptions<UserDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            // Seed inicial de datos.
            // Los hashes y la fecha son constantes a propósito: si aquí se llamara a
            // BC.HashPassword(...) o a DateTime.UtcNow, cada Add-Migration detectaría un
            // "cambio" en los datos semilla y generaría migraciones innecesarias.
            //   admin@example.com  -> contraseña Admin123!
            //   user@example.com   -> contraseña User123!
            var seedDate = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Email = "admin@example.com",
                    Name = "Admin",
                    LastName = "System",
                    PasswordHash = "$2b$11$WQZRitmqHKJqzxKVafNciu4BXBD9.DatJD24OE/IOVAQ3UtTsgtbW",
                    CreatedAt = seedDate,
                    IsActive = true
                },
                new User
                {
                    Id = 2,
                    Email = "user@example.com",
                    Name = "John",
                    LastName = "Doe",
                    PasswordHash = "$2b$11$mv.kqPFDh3C31GbdBSQYq.XwIax28XVkzk58HKjG8p6ZXuvrpV8yq",
                    CreatedAt = seedDate,
                    IsActive = true
                }
            );
        }
    }
}
