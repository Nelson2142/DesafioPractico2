using ClientesAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientesAPI.Data;

/// <summary>
/// Siembra de datos de prueba. Genera los clientes con un ciclo for y solo se
/// ejecuta cuando la tabla está vacía, así que es seguro dejarla activa siempre.
/// </summary>
public static class SembradorClientes
{
    /// <summary>Cantidad de clientes que se generan. Cámbiela si necesita más registros.</summary>
    public const int CantidadClientes = 1500;

    private static readonly string[] Nombres =
    [
        "José", "María", "Carlos", "Ana", "Luis", "Sofía", "Juan", "Gabriela",
        "Rodrigo", "Daniela", "Fernando", "Karla", "Mario", "Andrea", "Óscar"
    ];

    private static readonly string[] Apellidos =
    [
        "Meléndez", "Hernández", "Ramírez", "Martínez", "López", "González",
        "Rodríguez", "Pérez", "Sánchez", "Cruz", "Flores", "Rivera"
    ];

    private static readonly string[] Ciudades =
    [
        "San Salvador", "Soyapango", "Santa Tecla", "Mejicanos", "Apopa",
        "Ilopango", "Santa Ana", "San Miguel", "Sonsonate", "La Libertad"
    ];

    public static async Task SembrarAsync(ClientesDbContext contexto, ILogger logger)
    {
        if (await contexto.Clientes.AnyAsync())
        {
            logger.LogInformation("La tabla Clientes ya tiene datos. No se siembra de nuevo.");
            return;
        }

        var aleatorio = new Random(2026);
        var clientes = new List<Cliente>();

        for (var i = 1; i <= CantidadClientes; i++)
        {
            var nombre = Nombres[i % Nombres.Length];
            var apellido = Apellidos[i % Apellidos.Length];

            clientes.Add(new Cliente
            {
                Nombre = nombre,
                Apellido = apellido,
                Email = $"cliente{i}@correo.com",
                Telefono = $"7{aleatorio.Next(1000000, 9999999)}",
                Dui = $"{i:D8}-{i % 10}",
                Direccion = $"Colonia {Ciudades[i % Ciudades.Length]}, casa #{i}",
                FechaRegistro = DateTime.Now.AddDays(-aleatorio.Next(1, 700)),
                Activo = i % 10 != 0
            });
        }

        contexto.Clientes.AddRange(clientes);
        await contexto.SaveChangesAsync();

        logger.LogInformation("Siembra lista: {Cantidad} clientes insertados.", clientes.Count);
    }
}
