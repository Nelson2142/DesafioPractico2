using ClientesAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientesAPI.Data;

public class ClientesDbContext(DbContextOptions<ClientesDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var cliente = modelBuilder.Entity<Cliente>();

        cliente.ToTable("Clientes");
        cliente.HasKey(c => c.Id);

        cliente.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
        cliente.Property(c => c.Apellido).IsRequired().HasMaxLength(100);
        cliente.Property(c => c.Email).IsRequired().HasMaxLength(150);
        cliente.Property(c => c.Telefono).IsRequired().HasMaxLength(15);
        cliente.Property(c => c.Dui).IsRequired().HasMaxLength(10);
        cliente.Property(c => c.Direccion).HasMaxLength(200);
        cliente.Property(c => c.Activo).HasDefaultValue(true);

        // Integridad de datos: no se permiten correos ni DUI repetidos.
        cliente.HasIndex(c => c.Email).IsUnique().HasDatabaseName("IX_Clientes_Email");
        cliente.HasIndex(c => c.Dui).IsUnique().HasDatabaseName("IX_Clientes_Dui");

        // Indice de apoyo para las busquedas por apellido.
        cliente.HasIndex(c => c.Apellido).HasDatabaseName("IX_Clientes_Apellido");
    }
}
