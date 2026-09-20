using Microsoft.EntityFrameworkCore;
using PedidosAPI.Models;

namespace PedidosAPI.Data;

public class PedidosDbContext(DbContextOptions<PedidosDbContext> options) : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var pedido = modelBuilder.Entity<Pedido>();

        pedido.ToTable("Pedidos");
        pedido.HasKey(p => p.Id);

        pedido.Property(p => p.NumeroPedido).IsRequired().HasMaxLength(20);
        pedido.Property(p => p.Descripcion).IsRequired().HasMaxLength(200);
        pedido.Property(p => p.Estado).IsRequired().HasMaxLength(20);
        pedido.Property(p => p.Total).HasPrecision(18, 2);

        pedido.HasIndex(p => p.NumeroPedido).IsUnique().HasDatabaseName("IX_Pedidos_NumeroPedido");

        // Los pedidos se consultan principalmente por cliente: el indice es clave
        // para el rendimiento con 3,000 registros.
        pedido.HasIndex(p => p.ClienteId).HasDatabaseName("IX_Pedidos_ClienteId");
        pedido.HasIndex(p => p.Estado).HasDatabaseName("IX_Pedidos_Estado");
    }
}
