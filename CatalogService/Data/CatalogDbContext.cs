using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data
{
    public class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Name);
            });

            // Seed inicial de datos
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Laptop Dell XPS 13",
                    Description = "Ultrabook premium con pantalla InfinityEdge",
                    Price = 1299.99m,
                    Stock = 15,
                    Category = "Laptops",
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 2,
                    Name = "Mouse Logitech MX Master 3",
                    Description = "Mouse ergonómico inalámbrico profesional",
                    Price = 99.99m,
                    Stock = 50,
                    Category = "Accesorios",
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 3,
                    Name = "Teclado Mecánico Keychron K2",
                    Description = "Teclado mecánico inalámbrico 75%",
                    Price = 79.99m,
                    Stock = 30,
                    Category = "Accesorios",
                    CreatedAt = DateTime.UtcNow
                }
            );
        }
    }
}
