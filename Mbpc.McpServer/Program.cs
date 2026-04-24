using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ModelContextProtocol.Server; // Namespace oficial
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services;
using Mbpc.Api.Services.Auth;
using Mbpc.McpServer.Services;

var builder = Host.CreateApplicationBuilder(args);

// 1. Configuraciones desde appsettings.json
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<OracleDbSettings>(builder.Configuration.GetSection("OracleDbSettings"));

// 2. Persistencia MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// 3. Caché y Servicios de Negocio de tu API
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<ITipoCargaService, TipoCargaManagerService>();
builder.Services.AddScoped<IViajeService, ViajeManagerService>();
builder.Services.AddScoped<ICargaService, CargaManagerService>();
builder.Services.AddScoped<IConvoyManagerService, ConvoyManagerService>();
builder.Services.AddScoped<IBuqueService, BuqueManagerService>();

// 4. Identidad Falsa del Bot (Super Admin = CosteraId 0)
builder.Services.AddScoped<ICosteraUserContext, BotCosteraUserContext>();

// 5. Configuración Oficial del Servidor MCP
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // Escanea [McpServerToolType] mágicamente

var host = builder.Build();
await host.RunAsync();