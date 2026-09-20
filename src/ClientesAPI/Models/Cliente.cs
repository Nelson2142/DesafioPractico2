using System.ComponentModel.DataAnnotations;

namespace ClientesAPI.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    [StringLength(150, ErrorMessage = "El correo electrónico no puede exceder 150 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe contener exactamente 8 dígitos.")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DUI es obligatorio.")]
    [RegularExpression(@"^[0-9]{8}-[0-9]$", ErrorMessage = "El DUI debe tener el formato 01234567-8.")]
    public string Dui { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La dirección no puede exceder 200 caracteres.")]
    public string? Direccion { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public bool Activo { get; set; } = true;
}
