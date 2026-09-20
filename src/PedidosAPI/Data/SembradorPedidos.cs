using Microsoft.EntityFrameworkCore;
using PedidosAPI.Models;

namespace PedidosAPI.Data;

/// <summary>
/// Siembra de datos de prueba. Genera los pedidos con un ciclo for y solo se
/// ejecuta cuando la tabla está vacía.
/// </summary>
public static class SembradorPedidos
{
    /// <summary>Cantidad de pedidos que se generan.</summary>
    public const int CantidadPedidos = 1500;

    /// <summary>
    /// Debe coincidir con la cantidad de clientes sembrados por la API de Clientes,
    /// para que todos los pedidos apunten a un cliente que existe.
    /// </summary>
    public const int CantidadClientes = 1500;

    private static readonly string[] Productos =
    [
        "Laptop 14 pulgadas", "Monitor 24 pulgadas", "Teclado mecánico",
        "Mouse inalámbrico", "Impresora multifuncional", "Disco sólido 1TB",
        "Memoria RAM 16GB", "Silla ergonómica", "Escritorio de oficina",
        "Audífonos con micrófono", "Cámara web HD", "Tablet 10 pulgadas"
    ];

    private static readonly string[] Estados =
    [
        EstadosPedido.Pendiente, EstadosPedido.Procesando, EstadosPedido.Enviado,
        EstadosPedido.Entregado, EstadosPedido.Cancelado
    ];

    public static async Task SembrarAsync(PedidosDbContext contexto, ILogger logger)
    {
        if (await contexto.Pedidos.AnyAsync())
        {
            logger.LogInformation("La tabla Pedidos ya tiene datos. No se siembra de nuevo.");
            return;
        }

        var aleatorio = new Random(2026);
        var pedidos = new List<Pedido>();

        for (var i = 1; i <= CantidadPedidos; i++)
        {
            var cantidad = aleatorio.Next(1, 11);
            var precioUnitario = Math.Round(aleatorio.NextDouble() * 500 + 5, 2);

            pedidos.Add(new Pedido
            {
                // Los pedidos se reparten entre los clientes 1 .. CantidadClientes
                ClienteId = (i % CantidadClientes) + 1,
                NumeroPedido = $"PED-{i:D6}",
                Descripcion = Productos[i % Productos.Length],
                Cantidad = cantidad,
                Total = (decimal)Math.Round(cantidad * precioUnitario, 2),
                Estado = Estados[i % Estados.Length],
                FechaPedido = DateTime.Now.AddDays(-aleatorio.Next(1, 500))
            });
        }

        contexto.Pedidos.AddRange(pedidos);
        await contexto.SaveChangesAsync();

        logger.LogInformation("Siembra lista: {Cantidad} pedidos insertados.", pedidos.Count);
    }
}
