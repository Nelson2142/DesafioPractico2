using System.Text.Json;
using ClientesAPI.Common;

namespace ClientesAPI.Middlewares;

/// <summary>
/// Manejo centralizado de errores de la API: cualquier excepción no controlada
/// se traduce a una respuesta 500 con el mismo formato estándar.
/// </summary>
public class ManejoErroresMiddleware(RequestDelegate siguiente, ILogger<ManejoErroresMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (Exception excepcion)
        {
            logger.LogError(excepcion,
                "Error no controlado en {Metodo} {Ruta}",
                contexto.Request.Method, contexto.Request.Path);

            if (contexto.Response.HasStarted)
            {
                throw;
            }

            contexto.Response.Clear();
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            contexto.Response.ContentType = "application/json; charset=utf-8";

            var respuesta = RespuestaApi<object>.Fallo(
                "Ocurrió un error interno en la API de Clientes.",
                traceId: contexto.TraceIdentifier);

            await contexto.Response.WriteAsync(JsonSerializer.Serialize(respuesta,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
