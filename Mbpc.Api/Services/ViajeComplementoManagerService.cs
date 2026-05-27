using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public class ViajeComplementoManagerService : IViajeComplementoService
    {
        private readonly IMongoCollection<ViajeDetalleMongo> _detailsCollection;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ViajeComplementoManagerService> _logger;

        public ViajeComplementoManagerService(
            IMongoClient mongoClient,
            IOptions<MongoDbSettings> settings,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ViajeComplementoManagerService> logger)
        {
            var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
            this._detailsCollection = database.GetCollection<ViajeDetalleMongo>("details_mbpc");
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<ViajeComplementosDto?> ObtenerComplementosPorViajeIdAsync(string viajeId, CancellationToken ct = default)
        {
            _logger.LogInformation("Consultando complementos para el viaje {ViajeId} en MongoDB.", viajeId);
            
            var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.Id, viajeId);
            var documento = await _detailsCollection.Find(filtro).FirstOrDefaultAsync(ct);

            if (documento == null) return null;

            // Mapeo seguro hacia el DTO consolidado respetando tipado fuerte
            return new ViajeComplementosDto(
                ViajeId: documento.Id,
                NotasBitacora: documento.NotasBitacora?.Select(n => new NotaBitacoraDto(n.Id, n.Texto, n.Usuario, n.FechaHora, n.Categoria)).ToList() ?? new(),
                Agencias: documento.Agencias?.Select(a => new AgenciaDto(a.Rol, a.Nombre, a.Contacto)).ToList() ?? new(),
                DatosPbip: documento.DatosPbip != null ? new DatosPbipDto(documento.DatosPbip.ContactoOcpm, documento.DatosPbip.NroInmarsat, documento.DatosPbip.ArqueoBruto, documento.DatosPbip.NivelProteccion) : null
            );
        }

        public async Task<NotaBitacoraDto> AgregarNotaBitacoraAsync(string viajeId, AgregarNotaBitacoraDto dto, CancellationToken ct = default)
        {
            // ── BLINDAJE STATELESS DE IDENTIDAD VIA JWT CLAIMS ─────────────────
            var usuario = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) 
                          ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("username") 
                          ?? "Operador_PNA";

            var nuevaNota = new NotaBitacoraMongo // Asumiendo tu clase del Paso 1
            {
                Id = Guid.NewGuid().ToString(),
                Texto = dto.Texto.Trim(),
                Usuario = usuario,
                FechaHora = DateTime.UtcNow,
                Categoria = dto.Categoria
            };

            _logger.LogInformation("Inyectando nota de bitácora auditada para viaje {ViajeId} por usuario {Usuario}.", viajeId, usuario);

            var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.Id, viajeId);
            var update = Builders<ViajeDetalleMongo>.Update.Push("NotasBitacora", nuevaNota); // Atómico sin Split-Brain

            var resultado = await _detailsCollection.UpdateOneAsync(filtro, update, cancellationToken: ct);

            if (resultado.MatchedCount == 0)
            {
                _logger.LogError("Fallo de scoping: No se encontró el documento del viaje {ViajeId} para agregar la nota.", viajeId);
                throw new KeyNotFoundException($"No se encontró el detalle del viaje con ID {viajeId}");
            }

            return new NotaBitacoraDto(nuevaNota.Id, nuevaNota.Texto, nuevaNota.Usuario, nuevaNota.FechaHora, nuevaNota.Categoria);
        }

        public async Task ActualizarAgenciasAsync(string viajeId, List<AsignarAgenciaDto> dtos, CancellationToken ct = default)
        {
            _logger.LogInformation("Actualizando listado de agencias marítimas para el viaje {ViajeId}.", viajeId);

            var listaMongo = dtos.Select(dto => new AgenciaMongo 
            { 
                Rol = dto.Rol, 
                Nombre = dto.Nombre.Trim(), 
                Contacto = dto.Contacto.Trim() 
            }).ToList();

            var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.Id, viajeId);
            var update = Builders<ViajeDetalleMongo>.Update.Set("Agencias", listaMongo);

            await _detailsCollection.UpdateOneAsync(filtro, update, cancellationToken: ct);
        }

        public async Task ActualizarDatosPbipAsync(string viajeId, ActualizarDatosPbipDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Actualizando datos de protección marítima PBIP para el viaje {ViajeId}.", viajeId);

            var datosMongo = new DatosPbipMongo
            {
                ContactoOcpm = dto.ContactoOcpm.Trim(),
                NroInmarsat = dto.NroInmarsat.Trim(),
                ArqueoBruto = dto.ArqueoBruto,
                NivelProteccion = dto.NivelProteccion
            };

            var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.Id, viajeId);
            var update = Builders<ViajeDetalleMongo>.Update.Set("DatosPbip", datosMongo);

            await _detailsCollection.UpdateOneAsync(filtro, update, cancellationToken: ct);
        }
    }
}