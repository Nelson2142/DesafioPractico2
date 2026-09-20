using System.Text.Json;
using ApiGateway.Common;

namespace ApiGateway.Middlewares;

/// <summary>
/// Manejo centralizado de errores y respuestas estandarizadas del Gateway.
///
/// Atrapa cualquier excepción no controlada y, además, traduce a formato estándar
/// los errores que genera Ocelot (401 sin sesión, 404 de ruta no configurada,
/// 429 por límite de solicitudes, 502 si una API interna está apagada), que de lo
/// contrario llegarían al cliente con el cuerpo vacío.
/// </summary>
public class ManejoErroresMiddleware(
    RequestDelegate siguiente,
    ILogger<ManejoErroresMiddleware> logger)
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (Exception excepcion)
        {
            logger.LogError(excepcion,
                "Error no controlado en {Ruta}", contexto.Request.Path);

            if (!contexto.Response.HasStarted)
            {
                await EscribirAsync(contexto,
                    StatusCodes.Status500InternalServerError,
                    "Ocurrió un error interno en el API Gateway.");
            }

            return;
        }

        // Si el código es de error y todavía no se escribió nada en la respuesta,
        // significa que el error lo generó el Gateway y no una API interna
        // (las APIs internas ya responden con el formato estándar).
        if (contexto.Response.StatusCode >= 400 && !contexto.Response.HasStarted)
        {
            await EscribirAsync(contexto,
                contexto.Response.StatusCode,
                MensajePorCodigo(contexto.Response.StatusCode));
        }
    }

    private static string MensajePorCodigo(int codigo) => codigo switch
    {
        StatusCodes.Status401Unauthorized =>
            "No autenticado. Inicie sesión en /auth/login para consumir los servicios.",
        StatusCodes.Status403Forbidden =>
            "No cuenta con los permisos necesarios para este recurso.",
        StatusCodes.Status404NotFound =>
            "La ruta solicitada no está configurada en el API Gateway.",
        StatusCodes.Status405MethodNotAllowed =>
            "El método HTTP no está permitido para esta ruta.",
        StatusCodes.Status429TooManyRequests =>
            "Se excedió el límite de solicitudes permitidas. Intente en unos segundos.",
        StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable =>
            "El servicio interno no está disponible. Verifique que las APIs estén en ejecución.",
        StatusCodes.Status504GatewayTimeout =>
            "El servicio interno tardó demasiado en responder.",
        _ => "No fue posible completar la solicitud."
    };

    private static async Task EscribirAsync(HttpContext contexto, int codigo, string mensaje)
    {
        contexto.Response.StatusCode = codigo;
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var respuesta = RespuestaApi<object>.Fallo(mensaje, traceId: contexto.TraceIdentifier);

        await contexto.Response.WriteAsync(JsonSerializer.Serialize(respuesta, OpcionesJson));
    }
}
