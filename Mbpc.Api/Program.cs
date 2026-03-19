var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CONFIGURACIÓN DE MONGODB ---
// Mapeamos el JSON a nuestra clase fuertemente tipada
builder.Services.Configure<Mbpc.Api.Configuration.MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Registramos el cliente de Mongo como Singleton
builder.Services.AddSingleton<MongoDB.Driver.IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Mbpc.Api.Configuration.MongoDbSettings>>().Value;
    return new MongoDB.Driver.MongoClient(settings.ConnectionString);
});
// ---------------------------------

// --- ZONA DE REGISTRO DE SERVICIOS MOCK ---
builder.Services.AddSingleton<Mbpc.Api.Services.IViajeService, Mbpc.Api.Services.ViajeMongoService>();
builder.Services.AddSingleton<Mbpc.Api.Services.ICargaService, Mbpc.Api.Services.CargaMongoService>(); 
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