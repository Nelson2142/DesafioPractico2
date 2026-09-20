using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Auth;

/// <summary>
/// Crea los roles base y un usuario administrador para poder autenticarse
/// desde el primer arranque (útil para las capturas y las pruebas de carga).
/// </summary>
public static class SembradorIdentidad
{
    public static readonly string[] Roles = ["Administrador", "Usuario"];

    public static async Task SembrarAsync(
        UserManager<Usuario> usuarios,
        RoleManager<IdentityRole> roles,
        IConfiguration configuracion,
        ILogger logger)
    {
        foreach (var rol in Roles)
        {
            if (!await roles.RoleExistsAsync(rol))
            {
                await roles.CreateAsync(new IdentityRole(rol));
                logger.LogInformation("Rol {Rol} creado.", rol);
            }
        }

        var email = configuracion["Seguridad:UsuarioSemilla:Email"];
        var password = configuracion["Seguridad:UsuarioSemilla:Password"];
        var rolAsignado = configuracion["Seguridad:UsuarioSemilla:Rol"] ?? "Administrador";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await usuarios.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = "Administrador del sistema"
        };

        var resultado = await usuarios.CreateAsync(usuario, password);

        if (resultado.Succeeded)
        {
            await usuarios.AddToRoleAsync(usuario, rolAsignado);
            logger.LogInformation("Usuario administrador {Email} creado.", email);
        }
        else
        {
            logger.LogWarning(
                "No se pudo crear el usuario administrador: {Errores}",
                string.Join(" | ", resultado.Errors.Select(e => e.Description)));
        }
    }
}
