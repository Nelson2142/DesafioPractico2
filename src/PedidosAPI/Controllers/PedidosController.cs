using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedidosAPI.Common;
using PedidosAPI.Data;
using PedidosAPI.DTOs;
using PedidosAPI.Models;
using PedidosAPI.Services;

namespace PedidosAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PedidosController(
    PedidosDbContext contexto,
    ClientesApiClient clientesApi,
    ILogger<PedidosController> logger) : ControllerBase
{
    /// <summary>Obtiene la lista de todos los pedidos. Admite filtros y paginación opcional.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerPedidos(
        [FromQuery] string? estado = null,
        [FromQuery] int? clienteId = null,
        [FromQuery] int? pagina = null,
        [FromQuery] int tamanoPagina = 50)
    {
        var consulta = contexto.Pedidos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!EstadosPedido.EsValido(estado))
            {
                return BadRequest(RespuestaApi<object>.Fallo(
                    $"El estado '{estado}' no es válido. Valores permitidos: {string.Join(", ", EstadosPedido.Todos)}.",
                    traceId: HttpContext.TraceIdentifier));
            }

            var normalizado = EstadosPedido.Normalizar(estado);
            consulta = consulta.Where(p => p.Estado == normalizado);
        }

        if (clienteId.HasValue)
        {
            consulta = consulta.Where(p => p.ClienteId == clienteId.Value);
        }

        consulta = consulta.OrderBy(p => p.Id);

        // Sin el parámetro "pagina" se devuelve el listado completo: es el escenario
        // pesado que se usa para medir el efecto de la caché del API Gateway.
        if (!pagina.HasValue)
        {
            var todos = await consulta.ToListAsync();
            return Ok(RespuestaApi<List<Pedido>>.Ok(
                todos,
                $"Se encontraron {todos.Count} pedidos.",
                HttpContext.TraceIdentifier));
        }

        var paginaActual = pagina.Value < 1 ? 1 : pagina.Value;
        tamanoPagina = tamanoPagina is < 1 or > 500 ? 50 : tamanoPagina;

        var total = await consulta.CountAsync();
        var elementos = await consulta
            .Skip((paginaActual - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync();

        var datos = new
        {
            pagina = paginaActual,
            tamanoPagina,
            total,
            totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina),
            elementos
        };

        return Ok(RespuestaApi<object>.Ok(
            datos,
            $"Página {paginaActual} de pedidos.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Obtiene los detalles de un pedido específico mediante su ID.
    /// El detalle se enriquece con los datos del cliente consultados a la API de Clientes.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPedido(int id, CancellationToken cancelacion)
    {
        var pedido = await contexto.Pedidos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancelacion);

        if (pedido is null)
        {
            return NotFound(RespuestaApi<object>.Fallo(
                $"No existe un pedido con el ID {id}.",
                traceId: HttpContext.TraceIdentifier));
        }

        var verificacion = await clientesApi.VerificarClienteAsync(pedido.ClienteId, cancelacion);

        var detalle = new PedidoDetalleDto
        {
            Id = pedido.Id,
            NumeroPedido = pedido.NumeroPedido,
            Descripcion = pedido.Descripcion,
            Cantidad = pedido.Cantidad,
            Total = pedido.Total,
            Estado = pedido.Estado,
            FechaPedido = pedido.FechaPedido,
            ClienteId = pedido.ClienteId,
            Cliente = verificacion.Estado switch
            {
                EstadoVerificacion.Existe => (object?)verificacion.Cliente,
                EstadoVerificacion.NoExiste => new { mensaje = "El cliente ya no existe en la API de Clientes." },
                _ => new { mensaje = "La API de Clientes no está disponible en este momento." }
            }
        };

        return Ok(RespuestaApi<PedidoDetalleDto>.Ok(
            detalle,
            "Pedido encontrado.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Obtiene los pedidos asociados a un cliente específico.</summary>
    [HttpGet("cliente/{clienteId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ObtenerPedidosPorCliente(
        int clienteId, CancellationToken cancelacion)
    {
        // Comunicación entre servicios: se valida el cliente antes de responder,
        // para no devolver una lista vacía cuando en realidad el cliente no existe.
        var verificacion = await clientesApi.VerificarClienteAsync(clienteId, cancelacion);

        if (verificacion.Estado == EstadoVerificacion.NoExiste)
        {
            return NotFound(RespuestaApi<object>.Fallo(
                $"No existe un cliente con el ID {clienteId} en la API de Clientes.",
                traceId: HttpContext.TraceIdentifier));
        }

        var pedidos = await contexto.Pedidos.AsNoTracking()
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.FechaPedido)
            .ToListAsync(cancelacion);

        var datos = new
        {
            clienteId,
            cliente = verificacion.Cliente,
            totalPedidos = pedidos.Count,
            montoTotal = pedidos.Sum(p => p.Total),
            pedidos
        };

        var advertencia = verificacion.Estado == EstadoVerificacion.ServicioNoDisponible
            ? " (no se pudieron obtener los datos del cliente)"
            : string.Empty;

        return Ok(RespuestaApi<object>.Ok(
            datos,
            $"El cliente tiene {pedidos.Count} pedidos{advertencia}.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Crea un nuevo pedido asociado a un cliente existente.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CrearPedido(
        [FromBody] PedidoCrearDto dto, CancellationToken cancelacion)
    {
        if (dto.Estado is not null && !EstadosPedido.EsValido(dto.Estado))
        {
            return BadRequest(RespuestaApi<object>.Fallo(
                $"El estado '{dto.Estado}' no es válido. Valores permitidos: {string.Join(", ", EstadosPedido.Todos)}.",
                traceId: HttpContext.TraceIdentifier));
        }

        // Validación clave: el pedido no puede quedar huérfano.
        var verificacion = await clientesApi.VerificarClienteAsync(dto.ClienteId, cancelacion);

        if (verificacion.Estado == EstadoVerificacion.NoExiste)
        {
            return UnprocessableEntity(RespuestaApi<object>.Fallo(
                $"No se puede crear el pedido: el cliente {dto.ClienteId} no existe.",
                new Dictionary<string, string[]>
                {
                    ["clienteId"] = ["El cliente indicado no está registrado en la API de Clientes."]
                },
                HttpContext.TraceIdentifier));
        }

        if (verificacion.Estado == EstadoVerificacion.ServicioNoDisponible)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                RespuestaApi<object>.Fallo(
                    "No se puede validar el cliente porque la API de Clientes no está disponible. Intente más tarde.",
                    traceId: HttpContext.TraceIdentifier));
        }

        if (verificacion.Cliente is not null && !verificacion.Cliente.Activo)
        {
            return UnprocessableEntity(RespuestaApi<object>.Fallo(
                $"No se puede crear el pedido: el cliente {dto.ClienteId} está inactivo.",
                traceId: HttpContext.TraceIdentifier));
        }

        var pedido = new Pedido
        {
            ClienteId = dto.ClienteId,
            NumeroPedido = await GenerarNumeroPedidoAsync(cancelacion),
            Descripcion = dto.Descripcion.Trim(),
            Cantidad = dto.Cantidad,
            Total = decimal.Round(dto.Total, 2),
            Estado = dto.Estado is null
                ? EstadosPedido.Pendiente
                : EstadosPedido.Normalizar(dto.Estado),
            FechaPedido = DateTime.Now
        };

        contexto.Pedidos.Add(pedido);
        await contexto.SaveChangesAsync(cancelacion);

        logger.LogInformation(
            "Pedido {Numero} creado para el cliente {ClienteId}",
            pedido.NumeroPedido, pedido.ClienteId);

        var datos = new
        {
            pedido,
            cliente = verificacion.Cliente
        };

        return CreatedAtAction(
            nameof(ObtenerPedido),
            new { id = pedido.Id },
            RespuestaApi<object>.Ok(
                datos,
                "Pedido creado correctamente.",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Actualiza un pedido existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPedido(
        int id, [FromBody] PedidoActualizarDto dto, CancellationToken cancelacion)
    {
        var pedido = await contexto.Pedidos.FirstOrDefaultAsync(p => p.Id == id, cancelacion);

        if (pedido is null)
        {
            return NotFound(RespuestaApi<object>.Fallo(
                $"No existe un pedido con el ID {id}.",
                traceId: HttpContext.TraceIdentifier));
        }

        if (!EstadosPedido.EsValido(dto.Estado))
        {
            return BadRequest(RespuestaApi<object>.Fallo(
                $"El estado '{dto.Estado}' no es válido. Valores permitidos: {string.Join(", ", EstadosPedido.Todos)}.",
                traceId: HttpContext.TraceIdentifier));
        }

        // Regla de negocio: un pedido entregado o cancelado ya no se modifica.
        if (pedido.Estado is EstadosPedido.Entregado or EstadosPedido.Cancelado)
        {
            return Conflict(RespuestaApi<object>.Fallo(
                $"El pedido {pedido.NumeroPedido} está en estado '{pedido.Estado}' y no puede modificarse.",
                traceId: HttpContext.TraceIdentifier));
        }

        pedido.Descripcion = dto.Descripcion.Trim();
        pedido.Cantidad = dto.Cantidad;
        pedido.Total = decimal.Round(dto.Total, 2);
        pedido.Estado = EstadosPedido.Normalizar(dto.Estado);

        await contexto.SaveChangesAsync(cancelacion);

        logger.LogInformation("Pedido {Id} actualizado", id);

        return Ok(RespuestaApi<Pedido>.Ok(
            pedido,
            "Pedido actualizado correctamente.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Genera el siguiente número de pedido con el formato PED-000000.</summary>
    private async Task<string> GenerarNumeroPedidoAsync(CancellationToken cancelacion)
    {
        var ultimoId = await contexto.Pedidos
            .MaxAsync(p => (int?)p.Id, cancelacion) ?? 0;

        var consecutivo = ultimoId + 1;
        var numero = $"PED-{consecutivo:D6}";

        while (await contexto.Pedidos.AnyAsync(p => p.NumeroPedido == numero, cancelacion))
        {
            consecutivo++;
            numero = $"PED-{consecutivo:D6}";
        }

        return numero;
    }
}
