using System.Net;
using System.Text.Json;
using PedidosAPI.Common;

namespace PedidosAPI.Services;

/// <summary>Datos mínimos del cliente devueltos por la API de Clientes.</summary>
public class ClienteResumen
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

public enum EstadoVerificacion
{
    Existe,
    NoExiste,
    ServicioNoDisponible
}

public record ResultadoVerificacion(EstadoVerificacion Estado, ClienteResumen? Cliente);

/// <summary>
/// Comunicación entre servicios: la API de Pedidos consulta a la API de Clientes
/// para validar que el cliente exista antes de registrar o modificar un pedido.
/// </summary>
public class ClientesApiClient(HttpClient http, ILogger<ClientesApiClient> logger)
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResultadoVerificacion> VerificarClienteAsync(
        int clienteId, CancellationToken cancelacion = default)
    {
        try
        {
            var respuesta = await http.GetAsync($"api/clientes/{clienteId}/existe", cancelacion);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return new ResultadoVerificacion(EstadoVerificacion.NoExiste, null);
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "La API de Clientes respondió {Codigo} al verificar el cliente {ClienteId}.",
                    (int)respuesta.StatusCode, clienteId);

                return new ResultadoVerificacion(EstadoVerificacion.ServicioNoDisponible, null);
            }

            var contenido = await respuesta.Content.ReadAsStringAsync(cancelacion);
            var sobre = JsonSerializer.Deserialize<RespuestaApi<ClienteResumen>>(
                contenido, OpcionesJson);

            return new ResultadoVerificacion(EstadoVerificacion.Existe, sobre?.Datos);
        }
        catch (Exception excepcion) when (
            excepcion is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(excepcion,
                "No fue posible comunicarse con la API de Clientes para el cliente {ClienteId}.",
                clienteId);

            return new ResultadoVerificacion(EstadoVerificacion.ServicioNoDisponible, null);
        }
    }
}
