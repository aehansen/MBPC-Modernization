using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services;
using System.Text;
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
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Ingresá el token JWT. Ejemplo: Bearer {token}"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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

// ── JWT ──────────────────────────────────────────────────────────────────────
// Leemos la sección JwtSettings desde appsettings.json.
// La SigningKey se convierte a bytes y se usa para validar la firma HMAC-SHA256.
var jwtSection  = builder.Configuration.GetSection("JwtSettings");
var secretKey   = jwtSection["SecretKey"]   ?? throw new InvalidOperationException("JwtSettings:SecretKey no está configurada.");
var issuer      = jwtSection["Issuer"]      ?? throw new InvalidOperationException("JwtSettings:Issuer no está configurado.");
var audience    = jwtSection["Audience"]    ?? throw new InvalidOperationException("JwtSettings:Audience no está configurado.");

var signingKey  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = issuer,
        ValidAudience            = audience,
        IssuerSigningKey         = signingKey,
        // Tolerancia cero de desvío de reloj en producción; en desarrollo podés
        // relajarlo con ClockSkew = TimeSpan.FromMinutes(1).
        ClockSkew                = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

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

        var error  = feature?.Error;
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

// CORS debe ir antes de Authentication/Authorization
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Habilitar para producción cuando se configure HTTPS
// app.UseHttpsRedirection();

// ⚠️ Orden crítico: Authentication SIEMPRE antes de Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();