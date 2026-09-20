using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Auth;

/// <summary>
/// Usuario del sistema. Hereda de IdentityUser, que aporta el correo,
/// el hash de la contraseña, el sello de seguridad y el resto de campos
/// que ASP.NET Core Identity necesita.
/// </summary>
public class Usuario : IdentityUser
{
    public string? NombreCompleto { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}
