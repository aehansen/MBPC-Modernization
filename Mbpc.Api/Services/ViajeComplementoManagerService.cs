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

        // Método auxiliar para resolver el TravelId real (long) de forma resiliente (Oracle long vs Mongo ObjectId)
        private async Task<long?> ResolverTravelIdAsync(string viajeId, CancellationToken ct)
        {
            if (long.TryParse(viajeId, out long viajeIdLong))
            {
                return viajeIdLong;
            }

            // Fallback: Si no es long, asumimos que es el ObjectId (string) de MongoDB y buscamos en la colección last_mbpc
            var database = _detailsCollection.Database;
            var posicionesCollection = database.GetCollection<ViajePosicionMongo>("last_mbpc");
            
            var filtroPosicion = Builders<ViajePosicionMongo>.Filter.Eq(p => p.Id, viajeId);
            var posicion = await posicionesCollection.Find(filtroPosicion).FirstOrDefaultAsync(ct);

            if (posicion == null)
            {
                _logger.LogWarning("No se pudo resolver el TravelId de negocio para el ID provisto: {ViajeId}", viajeId);
                return null;
            }

            return posicion.TravelId;
        }

        public async Task<ViajeComplementosDto?> ObtenerComplementosPorViajeIdAsync(string viajeId, CancellationToken ct = default)
        {
            _logger.LogInformation("Resolviendo complementos para el viaje: {ViajeId}", viajeId);

            var targetTravelId = await ResolverTravelIdAsync(viajeId, ct);
            if (!targetTravelId.HasValue)
            {
                return null;
            }

            // Consultamos los complementos por el TravelId numérico unificado
            var filtroDetalle = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.IdViaje, targetTravelId.Value);
            var documento = await _detailsCollection.Find(filtroDetalle).FirstOrDefaultAsync(ct);

            if (documento == null)
            {
                _logger.LogInformation("Viaje {Id} sin detalles previos en details_mbpc. Retornando DTO vacío.", targetTravelId.Value);
                return new ViajeComplementosDto(
                    ViajeId: targetTravelId.Value.ToString(),
                    NotasBitacora: new(),
                    Agencias: new(),
                    DatosPbip: null
                );
            }

            return new ViajeComplementosDto(
                ViajeId: documento.IdViaje?.ToString() ?? targetTravelId.Value.ToString(),
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

            var targetTravelId = await ResolverTravelIdAsync(viajeId, ct);
            if (!targetTravelId.HasValue)
            {
                _logger.LogError("Fallo al agregar nota: No se pudo resolver el TravelId para el viaje {ViajeId}", viajeId);
                throw new KeyNotFoundException($"No se encontró el viaje correspondiente al ID {viajeId}");
            }

            var nuevaNota = new NotaBitacoraMongo
            {
                Id = Guid.NewGuid().ToString(),
                Texto = dto?.Texto?.Trim() ?? string.Empty,
                Usuario = usuario,
                FechaHora = DateTime.UtcNow,
                Categoria = dto?.Categoria?.Trim() ?? "Operacional"
            };

            _logger.LogInformation("Inyectando nota de bitácora para viaje {ViajeId} (TravelId: {TravelId}) por usuario {Usuario}.", viajeId, targetTravelId.Value, usuario);

            var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.IdViaje, targetTravelId.Value);
            var update = Builders<ViajeDetalleMongo>.Update.Push("NotasBitacora", nuevaNota);

            // IsUpsert = true garantiza la creación del documento raíz con el IdViaje unificado
            await _detailsCollection.UpdateOneAsync(filtro, update, new UpdateOptions { IsUpsert = true }, cancellationToken: ct);

            return new NotaBitacoraDto(nuevaNota.Id, nuevaNota.Texto, nuevaNota.Usuario, nuevaNota.FechaHora, nuevaNota.Categoria);
        }

        public async Task ActualizarAgenciasAsync(string viajeId, List<AsignarAgenciaDto> dtos, CancellationToken ct = default)
        {
            var targetTravelId = await ResolverTravelIdAsync(viajeId, ct);
            if (!targetTravelId.HasValue)
            {
                _logger.LogError("Fallo al actualizar agencias: No se pudo resolver el TravelId para el viaje {ViajeId}", viajeId);
                throw new KeyNotFoundException($"No se encontró el viaje correspondiente al ID {viajeId}");
            }

            _logger.LogInformation("Actualizando agencias para viaje {ViajeId} (TravelId: {TravelId}). Recibidos {Count} elementos.", 
                viajeId, targetTravelId.Value, dtos.Count);

            var listaMongo = dtos.Select(dto => new AgenciaMongo 
            { 
                Rol = dto.Rol, 
                Nombre = dto.Nombre.Trim(), 
                Contacto = dto.Contacto.Trim() 
            }).ToList();

            var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.IdViaje, targetTravelId.Value);
            var update = Builders<ViajeDetalleMongo>.Update.Set("Agencias", listaMongo);

            // IsUpsert = true garantiza la creación del documento raíz con el IdViaje unificado
            await _detailsCollection.UpdateOneAsync(filtro, update, new UpdateOptions { IsUpsert = true }, cancellationToken: ct);
        }

        public async Task ActualizarDatosPbipAsync(string viajeId, ActualizarDatosPbipDto dto, CancellationToken ct = default)
        {
            var targetTravelId = await ResolverTravelIdAsync(viajeId, ct);
            if (!targetTravelId.HasValue)
            {
                _logger.LogError("Fallo al actualizar datos PBIP: No se pudo resolver el TravelId para el viaje {ViajeId}", viajeId);
                throw new KeyNotFoundException($"No se encontró el viaje correspondiente al ID {viajeId}");
            }

            _logger.LogInformation("Actualizando datos PBIP para el viaje {ViajeId} (TravelId: {TravelId}).", viajeId, targetTravelId.Value);

            var datosMongo = new DatosPbipMongo
            {
                ContactoOcpm = dto.ContactoOcpm.Trim(),
                NroInmarsat = dto.NroInmarsat.Trim(),
                ArqueoBruto = dto.ArqueoBruto,
                NivelProteccion = dto.NivelProteccion
            };

            var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.IdViaje, targetTravelId.Value);
            var update = Builders<ViajeDetalleMongo>.Update.Set("DatosPbip", datosMongo);

            // IsUpsert = true garantiza la creación del documento raíz con el IdViaje unificado
            await _detailsCollection.UpdateOneAsync(filtro, update, new UpdateOptions { IsUpsert = true }, cancellationToken: ct);
        }
    }
}