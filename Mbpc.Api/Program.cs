using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
// Acá asumo que tenés tus servicios en este namespace. Si es otro, ajustalo:
using Mbpc.Api.Services; 

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Acepta mayúsculas o minúsculas
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CONFIGURACIÓN DE MONGODB ---
// 1. Mapeamos la configuración del appsettings a nuestra clase fuertemente tipada
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// 2. Registramos el MongoClient como Singleton
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});
// ---------------------------------

builder.Services.Configure<OracleDbSettings>(
    builder.Configuration.GetSection("OracleDbSettings"));

// --- ZONA DE REGISTRO DE SERVICIOS ---
// Por ahora dejamos los Mock, en el próximo paso actualizamos el ViajeMongoService
builder.Services.AddSingleton<IViajeService, ViajeManagerService>();
builder.Services.AddSingleton<ICargaService, CargaManagerService>(); 
// ------------------------------------------

// Permitimos que el frontend de React se comunique con esta API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowReact");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();