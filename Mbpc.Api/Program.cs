using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization; // <-- ¡Este era el using que faltaba!

var builder = WebApplication.CreateBuilder(args);

// ── Serialización JSON ──────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Restauramos tu CamelCase original (vital para React)
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        // Mantenemos el conversor de Enums
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// ── Oracle ──────────────────────────────────────────────────────────────────
builder.Services.Configure<OracleDbSettings>(
    builder.Configuration.GetSection("OracleDbSettings"));

// ── JWT ──────────────────────────────────────────────────────────────────────
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
        ClockSkew                = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ── Servicios de negocio ─────────────────────────────────────────────────────
builder.Services.AddScoped<IViajeService, ViajeManagerService>();
builder.Services.AddScoped<ICargaService, CargaManagerService>();

// ── IHttpContextAccessor ─────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ── CORS ─────────────────────────────────────────────────────────────────────
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
            detalle = app.Environment.IsDevelopment() ? error?.Message : null
        });
    });
});

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();