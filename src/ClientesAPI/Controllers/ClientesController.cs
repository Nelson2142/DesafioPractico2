using ClientesAPI.Common;
using ClientesAPI.Data;
using ClientesAPI.DTOs;
using ClientesAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientesAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ClientesController(
    ClientesDbContext contexto,
    ILogger<ClientesController> logger) : ControllerBase
{
    /// <summary>Obtiene la lista de todos los clientes. Admite búsqueda y paginación opcional.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerClientes(
        [FromQuery] string? buscar = null,
        [FromQuery] bool? activo = null,
        [FromQuery] int? pagina = null,
        [FromQuery] int tamanoPagina = 50)
    {
        var consulta = contexto.Clientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            consulta = consulta.Where(c =>
                c.Nombre.Contains(texto) ||
                c.Apellido.Contains(texto) ||
                c.Email.Contains(texto) ||
                c.Dui.Contains(texto));
        }

        if (activo.HasValue)
        {
            consulta = consulta.Where(c => c.Activo == activo.Value);
        }

        consulta = consulta.OrderBy(c => c.Id);

        // Sin el parámetro "pagina" se devuelve el listado completo
        // (es el escenario que se usa en las pruebas de rendimiento).
        if (!pagina.HasValue)
        {
            var todos = await consulta.ToListAsync();
            return Ok(RespuestaApi<List<Cliente>>.Ok(
                todos,
                $"Se encontraron {todos.Count} clientes.",
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
            $"Página {paginaActual} de clientes.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Obtiene los detalles de un cliente específico mediante su ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerCliente(int id)
    {
        var cliente = await contexto.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente is null)
        {
            return NotFound(RespuestaApi<object>.Fallo(
                $"No existe un cliente con el ID {id}.",
                traceId: HttpContext.TraceIdentifier));
        }

        return Ok(RespuestaApi<Cliente>.Ok(
            cliente,
            "Cliente encontrado.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Endpoint liviano usado por la API de Pedidos para validar la existencia
    /// del cliente antes de registrar un pedido (comunicación entre servicios).
    /// </summary>
    [HttpGet("{id:int}/existe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerificarExistencia(int id)
    {
        var resumen = await contexto.Clientes.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ClienteResumenDto
            {
                Id = c.Id,
                NombreCompleto = c.Nombre + " " + c.Apellido,
                Email = c.Email,
                Activo = c.Activo
            })
            .FirstOrDefaultAsync();

        if (resumen is null)
        {
            return NotFound(RespuestaApi<object>.Fallo(
                $"No existe un cliente con el ID {id}.",
                traceId: HttpContext.TraceIdentifier));
        }

        return Ok(RespuestaApi<ClienteResumenDto>.Ok(
            resumen,
            "El cliente existe.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Crea un nuevo cliente.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCliente([FromBody] ClienteCrearDto dto)
    {
        var errores = await ValidarUnicidadAsync(dto.Email, dto.Dui, null);
        if (errores.Count > 0)
        {
            return Conflict(RespuestaApi<object>.Fallo(
                "El cliente no pudo crearse porque existen datos duplicados.",
                errores,
                HttpContext.TraceIdentifier));
        }

        var cliente = new Cliente
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Telefono = dto.Telefono.Trim(),
            Dui = dto.Dui.Trim(),
            Direccion = dto.Direccion?.Trim(),
            Activo = dto.Activo,
            FechaRegistro = DateTime.Now
        };

        contexto.Clientes.Add(cliente);
        await contexto.SaveChangesAsync();

        logger.LogInformation("Cliente creado con ID {Id}", cliente.Id);

        return CreatedAtAction(
            nameof(ObtenerCliente),
            new { id = cliente.Id },
            RespuestaApi<Cliente>.Ok(
                cliente,
                "Cliente creado correctamente.",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Actualiza la información de un cliente existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCliente(int id, [FromBody] ClienteActualizarDto dto)
    {
        var cliente = await contexto.Clientes.FirstOrDefaultAsync(c => c.Id == id);

        if (cliente is null)
        {
            return NotFound(RespuestaApi<object>.Fallo(
                $"No existe un cliente con el ID {id}.",
                traceId: HttpContext.TraceIdentifier));
        }

        var errores = await ValidarUnicidadAsync(dto.Email, dto.Dui, id);
        if (errores.Count > 0)
        {
            return Conflict(RespuestaApi<object>.Fallo(
                "El cliente no pudo actualizarse porque existen datos duplicados.",
                errores,
                HttpContext.TraceIdentifier));
        }

        cliente.Nombre = dto.Nombre.Trim();
        cliente.Apellido = dto.Apellido.Trim();
        cliente.Email = dto.Email.Trim().ToLowerInvariant();
        cliente.Telefono = dto.Telefono.Trim();
        cliente.Dui = dto.Dui.Trim();
        cliente.Direccion = dto.Direccion?.Trim();
        cliente.Activo = dto.Activo;

        try
        {
            await contexto.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await contexto.Clientes.AnyAsync(c => c.Id == id))
            {
                return NotFound(RespuestaApi<object>.Fallo(
                    $"No existe un cliente con el ID {id}.",
                    traceId: HttpContext.TraceIdentifier));
            }

            throw;
        }

        logger.LogInformation("Cliente {Id} actualizado", id);

        return Ok(RespuestaApi<Cliente>.Ok(
            cliente,
            "Cliente actualizado correctamente.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Elimina un cliente.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarCliente(int id)
    {
        var cliente = await contexto.Clientes.FirstOrDefaultAsync(c => c.Id == id);

        if (cliente is null)
        {
            return NotFound(RespuestaApi<object>.Fallo(
                $"No existe un cliente con el ID {id}.",
                traceId: HttpContext.TraceIdentifier));
        }

        contexto.Clientes.Remove(cliente);
        await contexto.SaveChangesAsync();

        logger.LogInformation("Cliente {Id} eliminado", id);

        return Ok(RespuestaApi<object>.Ok(
            new { id },
            "Cliente eliminado correctamente.",
            HttpContext.TraceIdentifier));
    }

    /// <summary>Valida que el correo y el DUI no estén registrados en otro cliente.</summary>
    private async Task<Dictionary<string, string[]>> ValidarUnicidadAsync(
        string email, string dui, int? idActual)
    {
        var errores = new Dictionary<string, string[]>();
        var correo = email.Trim().ToLowerInvariant();
        var documento = dui.Trim();

        var consultaEmail = contexto.Clientes.Where(c => c.Email == correo);
        var consultaDui = contexto.Clientes.Where(c => c.Dui == documento);

        if (idActual.HasValue)
        {
            var id = idActual.Value;
            consultaEmail = consultaEmail.Where(c => c.Id != id);
            consultaDui = consultaDui.Where(c => c.Id != id);
        }

        if (await consultaEmail.AnyAsync())
        {
            errores["email"] = ["Ya existe un cliente registrado con este correo electrónico."];
        }

        if (await consultaDui.AnyAsync())
        {
            errores["dui"] = ["Ya existe un cliente registrado con este DUI."];
        }

        return errores;
    }
}
