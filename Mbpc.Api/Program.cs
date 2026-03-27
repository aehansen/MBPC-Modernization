using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Serialización JSON ──────────────────────────────────────────────────────
// CamelCase para que el frontend React reciba los campos correctamente
// (buque, ruta, estadoActual en lugar de Buque, Ruta, EstadoActual)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddDistributedMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── MongoDB ─────────────────────────────────────────────────────────────────
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// MongoClient es thread-safe por diseño → Singleton correcto
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// ── Oracle ──────────────────────────────────────────────────────────────────
builder.Services.Configure<OracleDbSettings>(
    builder.Configuration.GetSection("OracleDbSettings"));

// ── Servicios de negocio ─────────────────────────────────────────────────────
// Scoped (por request) para evitar problemas si en el futuro se agrega
// alguna dependencia con scope de request (ej: IHttpContextAccessor).
// MongoClient sigue siendo Singleton arriba, así que no hay problema.
builder.Services.AddScoped<IViajeService, ViajeManagerService>();
builder.Services.AddScoped<ICargaService, CargaManagerService>();

// ── CORS ─────────────────────────────────────────────────────────────────────
// Origen leído desde configuración para no hardcodear en producción.
// En appsettings.Development.json: "AllowedOrigins": "http://localhost:5173"
// En appsettings.Production.json:  "AllowedOrigins": "https://mbpc.prefectura.gob.ar"
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration["AllowedOrigins"]
            ?? "http://localhost:5173";

        policy.WithOrigins(allowedOrigins.Split(','))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ── Middleware de excepciones global ─────────────────────────────────────────
// Captura cualquier excepción no manejada y devuelve un JSON limpio
// sin exponer stack traces al cliente.
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var feature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        var error = feature?.Error;
        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        logger.LogError(error, "Excepción no manejada");

        await context.Response.WriteAsJsonAsync(new
        {
            mensaje = "Ocurrió un error interno. Por favor contacte al administrador.",
            // Solo mostramos el tipo de error en desarrollo
            detalle = app.Environment.IsDevelopment() ? error?.Message : null
        });
    });
});

// CORS debe ir antes de Authorization
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Habilitar para producción cuando se configure HTTPS
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();
