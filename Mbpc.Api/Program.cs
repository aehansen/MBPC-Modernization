// Archivo: Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services;
using Mbpc.Api.Services.Auth;
using Mbpc.Api.Workers;
using Oracle.ManagedDataAccess.Client;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── Serialización JSON ──────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy        = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITipoCargaService, TipoCargaManagerService>();
builder.Services.AddScoped<IChatService, ChatManagerService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<IViajeComplementoService, ViajeComplementoManagerService>();
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

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return client.GetDatabase(settings.DatabaseName);
});

// ── Oracle ──────────────────────────────────────────────────────────────────
builder.Services.Configure<OracleDbSettings>(
    builder.Configuration.GetSection("OracleDbSettings"));

var oracleSettings = builder.Configuration
    .GetSection("OracleDbSettings")
    .Get<OracleDbSettings>();

if (!string.IsNullOrWhiteSpace(oracleSettings?.TnsAdminPath))
{
    OracleConfiguration.TnsAdmin = oracleSettings.TnsAdminPath;
}

// ── JWT ──────────────────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secretKey  = jwtSection["SecretKey"]  ?? throw new InvalidOperationException("JwtSettings:SecretKey no está configurada.");
var issuer     = jwtSection["Issuer"]     ?? throw new InvalidOperationException("JwtSettings:Issuer no está configurado.");
var audience   = jwtSection["Audience"]   ?? throw new InvalidOperationException("JwtSettings:Audience no está configurado.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

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
builder.Services.AddScoped<IConvoyManagerService, ConvoyManagerService>();
builder.Services.AddScoped<IBuqueService, BuqueManagerService>();
builder.Services.AddScoped<ICosteraService, CosteraManagerService>();
builder.Services.AddScoped<ICatalogoService, CatalogoManagerService>();
builder.Services.AddScoped<IReporteService, ReporteManagerService>();
builder.Services.AddScoped<IQueryBuilderService, QueryBuilderManagerService>();
builder.Services.AddScoped<IInspeccionService, InspeccionManagerService>();
builder.Services.AddScoped<IReconciliacionService, ReconciliacionManagerService>();
builder.Services.AddScoped<IAisIngestionService, AisIngestionService>();

// ── Background Workers / Hosted Services ──────────────────────────────────────
builder.Services.AddHostedService<ReconciliacionEspacialWorker>();

// ── Servicio de Chat / IA ────────────────────────────────────────────────────
builder.Services.AddScoped<IChatService, ChatManagerService>();

// ── IHttpContextAccessor y Dependencias transversales ────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICosteraUserContext, HttpCosteraUserContext>();

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

Mbpc.Api.Configuration.MongoMappingConfig.RegisterMappings();

var app = builder.Build();

// ── Middleware de excepciones global ─────────────────────────────────────────
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        var error  = feature?.Error;
        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        logger.LogError(error, "Excepción no manejada");

        context.Response.ContentType = "application/json";

        if (error is InvalidOperationException)
        {
            context.Response.StatusCode = 422;
            await context.Response.WriteAsJsonAsync(new
            {
                mensaje = error.Message,
                detalle = error.Message
            });
            return;
        }

        context.Response.StatusCode = 500;
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

// ── Semillero de datos de prueba para buques en Gualeguaychú (id: 425) ─────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var mongoSettings = scope.ServiceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
        var collectionPos = database.GetCollection<Mbpc.Api.Models.Mongo.ViajePosicionMongo>(mongoSettings.LastMbpcCollectionName);
        var collectionDet = database.GetCollection<Mbpc.Api.Models.Mongo.ViajeDetalleMongo>(mongoSettings.DetailsMbpcCollectionName);

        var seedVessels = new[]
        {
            new { Name = "MAREM", Imo = (int?)9545077, Matricula = (string?)null, TravelId = 9000000L },
            new { Name = "ARGENMAR MISTRAL", Imo = (int?)9498937, Matricula = (string?)"03166", TravelId = 9000001L },
            new { Name = "NOEMI G", Imo = (int?)null, Matricula = (string?)"01376", TravelId = 9000002L },
            new { Name = "MAMACOTA", Imo = (int?)null, Matricula = (string?)"0821M", TravelId = 9000003L },
            new { Name = "YORK", Imo = (int?)1020514, Matricula = (string?)null, TravelId = 9000004L }
        };

        foreach (var v in seedVessels)
        {
            var exists = collectionPos.Find(p => p.VesselName == v.Name).Any();
            if (!exists)
            {
                var pos = new Mbpc.Api.Models.Mongo.ViajePosicionMongo
                {
                    TravelId = v.TravelId,
                    VesselName = v.Name,
                    Imo = v.Imo,
                    CallSign = v.Matricula,
                    NavegationStatusDesc = "Amarrado",
                    MsgTime = DateTime.UtcNow,
                    Latitude = -33.0095, // Coordenadas sobre el río Gualeguaychú
                    Longitude = -58.5085,
                    Origin = "Gualeguaychú",
                    Destination = "Buenos Aires",
                    CosteraId = 425
                };
                collectionPos.InsertOne(pos);

                var det = new Mbpc.Api.Models.Mongo.ViajeDetalleMongo
                {
                    IdViaje = v.TravelId,
                    VesselName = v.Name,
                    Origin = "Gualeguaychú",
                    Destination = "Buenos Aires",
                    CosteraId = 425,
                    Etapas = new List<Mbpc.Api.Models.Mongo.EtapaMongo>
                    {
                        new Mbpc.Api.Models.Mongo.EtapaMongo
                        {
                            EtapaId = 1,
                            FechaInicio = DateTime.UtcNow,
                            Barcazas = new List<Mbpc.Api.Models.Mongo.BarcazaMongo>()
                        }
                    }
                };
                collectionDet.InsertOne(det);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al sembrar viajes de prueba en Gualeguaychú: {ex.Message}");
    }
}

app.Run();
