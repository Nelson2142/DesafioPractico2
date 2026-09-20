using System.ComponentModel.DataAnnotations;

namespace ClientesAPI.DTOs;

/// <summary>Datos aceptados para crear un cliente. El Id y la fecha los asigna la API.</summary>
public class ClienteCrearDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe contener exactamente 8 dígitos.")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DUI es obligatorio.")]
    [RegularExpression(@"^[0-9]{8}-[0-9]$", ErrorMessage = "El DUI debe tener el formato 01234567-8.")]
    public string Dui { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>Datos aceptados para actualizar un cliente existente.</summary>
public class ClienteActualizarDto : ClienteCrearDto
{
}

/// <summary>Respuesta liviana usada por la API de Pedidos para validar un cliente.</summary>
public class ClienteResumenDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
