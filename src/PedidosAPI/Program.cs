using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedidosAPI.Common;
using PedidosAPI.Data;
using PedidosAPI.Middlewares;
using PedidosAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddDbContext<PedidosDbContext>(opciones =>
    opciones.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Comunicación entre servicios: cliente HTTP hacia la API de Clientes.
builder.Services.AddHttpClient<ClientesApiClient>(cliente =>
{
    cliente.BaseAddress = new Uri(
        builder.Configuration["ServiciosExternos:ClientesApi"] ?? "http://localhost:5101/");
    cliente.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
    {
        opciones.InvalidModelStateResponseFactory = contexto =>
        {
            var errores = contexto.ModelState
                .Where(entrada => entrada.Value is not null && entrada.Value.Errors.Count > 0)
                .ToDictionary(
                    entrada => entrada.Key,
                    entrada => entrada.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return new BadRequestObjectResult(RespuestaApi<object>.Fallo(
                "Los datos enviados no son válidos.", errores));
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Base de datos: crea el esquema si hace falta y siembra los datos de prueba
using (var alcance = app.Services.CreateScope())
{
    var contexto = alcance.ServiceProvider.GetRequiredService<PedidosDbContext>();

    if (contexto.Database.GetMigrations().Any())
    {
        contexto.Database.Migrate();
    }
    else
    {
        contexto.Database.EnsureCreated();
    }

    await SembradorPedidos.SembrarAsync(contexto, app.Logger);
}

// Pipeline
app.UseMiddleware<ManejoErroresMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { servicio = "PedidosAPI", estado = "activo" }));

app.MapControllers();

app.Run();
