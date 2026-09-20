using ClientesAPI.Common;
using ClientesAPI.Data;
using ClientesAPI.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddDbContext<ClientesDbContext>(opciones =>
    opciones.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
    {
        // Los errores de validación también salen con el formato estándar.
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
    var contexto = alcance.ServiceProvider.GetRequiredService<ClientesDbContext>();

    if (contexto.Database.GetMigrations().Any())
    {
        contexto.Database.Migrate();
    }
    else
    {
        contexto.Database.EnsureCreated();
    }

    await SembradorClientes.SembrarAsync(contexto, app.Logger);
}

// Pipeline
app.UseMiddleware<ManejoErroresMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { servicio = "ClientesAPI", estado = "activo" }));

app.MapControllers();

app.Run();
