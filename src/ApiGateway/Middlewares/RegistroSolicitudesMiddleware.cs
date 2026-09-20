using System.Diagnostics;

namespace ApiGateway.Middlewares;

/// <summary>
/// Registro de solicitudes y respuestas para auditoría: deja en el log el método,
/// la ruta, el usuario, el código de respuesta y cuánto tardó cada solicitud.
/// El tiempo en milisegundos es, además, la evidencia del efecto de la caché.
/// </summary>
public class RegistroSolicitudesMiddleware(
    RequestDelegate siguiente,
    ILogger<RegistroSolicitudesMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        var cronometro = Stopwatch.StartNew();

        await siguiente(contexto);

        cronometro.Stop();

        var usuario = contexto.User.Identity?.IsAuthenticated == true
            ? contexto.User.Identity.Name
            : "anónimo";

        logger.LogInformation(
            "{Metodo} {Ruta} -> {Codigo} en {Duracion} ms | usuario: {Usuario}",
            contexto.Request.Method,
            contexto.Request.Path + contexto.Request.QueryString,
            contexto.Response.StatusCode,
            cronometro.ElapsedMilliseconds,
            usuario);
    }
}
