using ApiGateway.Auth;
using ApiGateway.Middlewares;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configuración de las rutas de Ocelot
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Base de datos de usuarios (Identity)
builder.Services.AddDbContext<AuthDbContext>(opciones =>
    opciones.UseSqlServer(builder.Configuration.GetConnectionString("AuthConnection")));

// Autenticación con cookie de Identity
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, opciones =>
    {
        opciones.Cookie.Name = "DesafioGateway.Auth";
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);

        // El Gateway es una API: responde 401 en lugar de redirigir a una página.
        opciones.Events.OnRedirectToLogin = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        opciones.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddIdentityCore<Usuario>(opciones =>
{
    opciones.Password.RequiredLength = 8;
    opciones.Password.RequireNonAlphanumeric = false;
    opciones.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddSignInManager();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Ocelot con su mecanismo de caché en memoria
builder.Services.AddOcelot(builder.Configuration)
    .AddCacheManager(opciones => opciones.WithDictionaryHandle());

var app = builder.Build();

// Base de datos de Identity: esquema, roles y usuario administrador
using (var alcance = app.Services.CreateScope())
{
    var contexto = alcance.ServiceProvider.GetRequiredService<AuthDbContext>();

    if (contexto.Database.GetMigrations().Any())
    {
        contexto.Database.Migrate();
    }
    else
    {
        contexto.Database.EnsureCreated();
    }

    await SembradorIdentidad.SembrarAsync(
        alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>(),
        alcance.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        app.Configuration,
        app.Logger);
}

// Pipeline
app.UseMiddleware<RegistroSolicitudesMiddleware>();   // auditoría
app.UseMiddleware<ManejoErroresMiddleware>();         // errores y respuestas estándar

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { servicio = "ApiGateway", estado = "activo" }));

// Ocelot atiende todo lo que no sea una ruta propia del Gateway
app.UseWhen(EsRutaReenviada, rama => rama.UseOcelot().Wait());

app.Run();

// Las rutas propias del Gateway (login, swagger, health) no pasan por Ocelot.
static bool EsRutaReenviada(HttpContext contexto)
{
    string[] rutasPropias = ["/auth", "/swagger", "/health", "/favicon.ico"];

    var ruta = contexto.Request.Path.Value ?? string.Empty;

    if (rutasPropias.Any(propia => ruta.StartsWith(propia, StringComparison.OrdinalIgnoreCase)))
    {
        return false;
    }

    return contexto.GetEndpoint() is null;
}
