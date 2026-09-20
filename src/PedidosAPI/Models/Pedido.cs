using System.ComponentModel.DataAnnotations;

namespace PedidosAPI.Models;

public class Pedido
{
    public int Id { get; set; }

    /// <summary>Identificador del cliente dueño del pedido (API de Clientes).</summary>
    [Required(ErrorMessage = "El ClienteId es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El ClienteId debe ser mayor que cero.")]
    public int ClienteId { get; set; }

    [Required]
    [StringLength(20)]
    public string NumeroPedido { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción del pedido es obligatoria.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "La descripción debe tener entre 3 y 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "La cantidad debe estar entre 1 y 1000.")]
    public int Cantidad { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "El total debe ser mayor que cero.")]
    public decimal Total { get; set; }

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = EstadosPedido.Pendiente;

    public DateTime FechaPedido { get; set; } = DateTime.Now;
}

/// <summary>Estados válidos de un pedido.</summary>
public static class EstadosPedido
{
    public const string Pendiente = "Pendiente";
    public const string Procesando = "Procesando";
    public const string Enviado = "Enviado";
    public const string Entregado = "Entregado";
    public const string Cancelado = "Cancelado";

    public static readonly string[] Todos =
    [
        Pendiente, Procesando, Enviado, Entregado, Cancelado
    ];

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) &&
        Todos.Any(e => e.Equals(estado, StringComparison.OrdinalIgnoreCase));

    public static string Normalizar(string estado) =>
        Todos.First(e => e.Equals(estado, StringComparison.OrdinalIgnoreCase));
}
