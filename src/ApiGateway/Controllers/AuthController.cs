using ApiGateway.Auth;
using ApiGateway.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>
/// Autenticación del API Gateway (ASP.NET Core Identity).
/// Estas rutas son las únicas que no se reenvían a las APIs internas:
/// el Gateway las atiende directamente para emitir y cerrar la sesión.
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController(
    UserManager<Usuario> usuarios,
    SignInManager<Usuario> sesiones,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Registra un nuevo usuario del sistema.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar([FromBody] RegistroDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(RespuestaApi<object>.Fallo(
                "Los datos enviados no son válidos.",
                ModelState.ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()),
                HttpContext.TraceIdentifier));
        }

        var usuario = new Usuario
        {
            UserName = dto.Email,
            Email = dto.Email,
            EmailConfirmed = true,
            NombreCompleto = dto.NombreCompleto
        };

        var resultado = await usuarios.CreateAsync(usuario, dto.Password);

        if (!resultado.Succeeded)
        {
            return BadRequest(RespuestaApi<object>.Fallo(
                "No fue posible registrar el usuario.",
                resultado.Errors.Select(e => e.Description).ToArray(),
                HttpContext.TraceIdentifier));
        }

        await usuarios.AddToRoleAsync(usuario, "Usuario");
        logger.LogInformation("Usuario registrado: {Email}", dto.Email);

        return StatusCode(StatusCodes.Status201Created, RespuestaApi<object>.Ok(
            new { usuario.Id, usuario.Email },
            "Usuario registrado correctamente.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Inicia sesión. Devuelve la cookie de autenticación que el Gateway
    /// valida en cada solicitud antes de permitir el consumo de los servicios.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IniciarSesion([FromBody] LoginDto dto)
    {
        var usuario = await usuarios.FindByEmailAsync(dto.Email);

        if (usuario is null)
        {
            logger.LogWarning("Intento de inicio de sesión con correo inexistente: {Email}", dto.Email);

            return Unauthorized(RespuestaApi<object>.Fallo(
                "Las credenciales proporcionadas no son válidas.",
                traceId: HttpContext.TraceIdentifier));
        }

        var resultado = await sesiones.PasswordSignInAsync(
            usuario.UserName!, dto.Password, dto.Recordarme, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
        {
            return Unauthorized(RespuestaApi<object>.Fallo(
                "La cuenta está bloqueada temporalmente por intentos fallidos.",
                traceId: HttpContext.TraceIdentifier));
        }

        if (!resultado.Succeeded)
        {
            return Unauthorized(RespuestaApi<object>.Fallo(
                "Las credenciales proporcionadas no son válidas.",
                traceId: HttpContext.TraceIdentifier));
        }

        var roles = await usuarios.GetRolesAsync(usuario);
        logger.LogInformation("Inicio de sesión exitoso: {Email}", usuario.Email);

        return Ok(RespuestaApi<object>.Ok(
            new
            {
                usuario.Id,
                usuario.Email,
                usuario.NombreCompleto,
                roles
            },
            "Sesión iniciada correctamente. La cookie de autenticación ya está activa.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Cierra la sesión actual.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CerrarSesion()
    {
        await sesiones.SignOutAsync();

        return Ok(RespuestaApi<object>.Ok(
            new { },
            "Sesión finalizada.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Devuelve los datos del usuario autenticado. Sirve para verificar la sesión.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObtenerSesion()
    {
        var usuario = await usuarios.GetUserAsync(User);

        if (usuario is null)
        {
            return Unauthorized(RespuestaApi<object>.Fallo(
                "La sesión no es válida.",
                traceId: HttpContext.TraceIdentifier));
        }

        var roles = await usuarios.GetRolesAsync(usuario);

        return Ok(RespuestaApi<object>.Ok(
            new
            {
                usuario.Id,
                usuario.Email,
                usuario.NombreCompleto,
                roles,
                autenticado = true
            },
            "Sesión activa.",
            HttpContext.TraceIdentifier));
    }
}
