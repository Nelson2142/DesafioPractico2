using System.ComponentModel.DataAnnotations;

namespace PedidosAPI.DTOs;

public class PedidoCrearDto
{
    [Required(ErrorMessage = "El ClienteId es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El ClienteId debe ser mayor que cero.")]
    public int ClienteId { get; set; }

    [Required(ErrorMessage = "La descripción del pedido es obligatoria.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "La descripción debe tener entre 3 y 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "La cantidad debe estar entre 1 y 1000.")]
    public int Cantidad { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "El total debe ser mayor que cero.")]
    public decimal Total { get; set; }

    /// <summary>Opcional. Si no se envía, el pedido se crea como "Pendiente".</summary>
    public string? Estado { get; set; }
}

public class PedidoActualizarDto
{
    [Required(ErrorMessage = "La descripción del pedido es obligatoria.")]
    [StringLength(200, MinimumLength = 3)]
    public string Descripcion { get; set; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "La cantidad debe estar entre 1 y 1000.")]
    public int Cantidad { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "El total debe ser mayor que cero.")]
    public decimal Total { get; set; }

    [Required(ErrorMessage = "El estado es obligatorio.")]
    public string Estado { get; set; } = string.Empty;
}

/// <summary>Detalle de un pedido enriquecido con los datos del cliente.</summary>
public class PedidoDetalleDto
{
    public int Id { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaPedido { get; set; }
    public int ClienteId { get; set; }

    /// <summary>Datos obtenidos de la API de Clientes. Es null si el servicio no respondió.</summary>
    public object? Cliente { get; set; }
}
