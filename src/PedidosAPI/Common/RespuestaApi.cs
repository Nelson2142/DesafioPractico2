namespace PedidosAPI.Common;

/// <summary>
/// Sobre (envelope) unico para todas las respuestas de la API.
/// Permite que el API Gateway devuelva siempre la misma estructura al cliente,
/// tanto en operaciones exitosas como en errores.
/// </summary>
public class RespuestaApi<T>
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public T? Datos { get; set; }
    public object? Errores { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public static RespuestaApi<T> Ok(
        T datos,
        string mensaje = "Operación realizada correctamente",
        string? traceId = null) => new()
        {
            Exito = true,
            Mensaje = mensaje,
            Datos = datos,
            TraceId = traceId
        };

    public static RespuestaApi<T> Fallo(
        string mensaje,
        object? errores = null,
        string? traceId = null) => new()
        {
            Exito = false,
            Mensaje = mensaje,
            Errores = errores,
            TraceId = traceId
        };
}
