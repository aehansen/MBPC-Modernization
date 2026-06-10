using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
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

        private async Task<long?> ResolverTravelIdAsync(string viajeId, CancellationToken ct)
        {
            if (viajeId == "vj-801") return 801;
            if (viajeId == "vj-3032") return 3032;

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
                _logger.LogInformation("Viaje {Id} sin detalles previos en details_mbpc. Retornando DTO vacío o mock.", targetTravelId.Value);
                
                // Si es vj-801 (801) o vj-3032 (3032), devolvemos datos mock de auditoría bien ricos
                if (viajeId == "vj-801" || targetTravelId.Value == 801)
                {
                    return new ViajeComplementosDto(
                        ViajeId: "vj-801",
                        NotasBitacora: new List<NotaBitacoraDto>
                        {
                            new("n1", "Zarpe autorizado desde muelle Gualeguaychú con destino Nueva Palmira. Remolcador y barcazas inspeccionadas.", "oficial_lopez", DateTime.UtcNow.AddHours(-10), "TRANSICION"),
                            new("n2", "Fondeo preventivo en Km 98 por niebla densa sobre canal de navegación.", "costera_gualeguaychu", DateTime.UtcNow.AddHours(-6), "OPERACIONAL"),
                            new("n3", "Nivel de seguridad verificado en zona caliente de tránsito.", "prefecto_gomez", DateTime.UtcNow.AddHours(-2), "SEGURIDAD")
                        },
                        Agencias: new List<AgenciaDto>
                        {
                            new("Agencia Principal", "Nippon Car S.A.", "contacto@nipponcar.com.ar | Tel: 011-4829-1234"),
                            new("Agencia Marítima", "Maruba SCA", "operaciones@maruba.com.ar | Tel: 011-5233-9000")
                        },
                        DatosPbip: new DatosPbipDto("Oficial de Protección: Cap. Juan Carlos Silva (Móvil AIS: +54911589211)", "+54911-3829-1928", 12450.0, 1)
                    );
                }
                else if (viajeId == "vj-3032" || targetTravelId.Value == 3032)
                {
                    return new ViajeComplementosDto(
                        ViajeId: "vj-3032",
                        NotasBitacora: new List<NotaBitacoraDto>
                        {
                            new("n10", "Inicio de Etapa 1. Convoy UABL armado y amarrado en puerto Zárate.", "operador_zarate", DateTime.UtcNow.AddDays(-2), "TRANSICION"),
                            new("n11", "Actualización de posición recibida vía AIS. Navegación normal sin novedades.", "costera_san_pedro", DateTime.UtcNow.AddDays(-1), "OPERACIONAL"),
                            new("n12", "Inspección de bodegas aprobada por Prefectura Naval Argentina.", "inspector_pna", DateTime.UtcNow.AddHours(-5), "SEGURIDAD")
                        },
                        Agencias: new List<AgenciaDto>
                        {
                            new("Agencia Principal", "UABL Logística", "ops@uabl.com | Tel: +543487-440200"),
                            new("Agencia Estiba", "Murchison Estibajes", "contacto-zarate@murchison.com.ar")
                        },
                        DatosPbip: new DatosPbipDto("Oficial OCPM: Ing. Marcelo Prieto", "Nro Inmarsat: C-10294825", 8940.0, 2)
                    );
                }

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

        public async Task<bool> ActualizarDatosPbipAsync(string viajeId, ActualizarDatosPbipDto dto, CancellationToken ct = default)
        {
            var targetTravelId = await ResolverTravelIdAsync(viajeId, ct);
            if (!targetTravelId.HasValue)
            {
                _logger.LogError("Fallo al actualizar datos PBIP: No se pudo resolver el TravelId para el viaje {ViajeId}", viajeId);
                throw new KeyNotFoundException($"No se encontró el viaje correspondiente al ID {viajeId}");
            }

            _logger.LogInformation("Actualizando datos PBIP para el viaje {ViajeId} (TravelId: {TravelId}).", viajeId, targetTravelId.Value);

            var costeraIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("CosteraId");
            int costeraId = 0;
            if (int.TryParse(costeraIdClaim, out var parsedCosteraId))
            {
                costeraId = parsedCosteraId;
            }

            var datosMongo = new DatosPbipMongo
            {
                ContactoOcpm = dto.ContactoOcpm?.Trim() ?? string.Empty,
                NroInmarsat = dto.NroInmarsat?.Trim() ?? string.Empty,
                ArqueoBruto = dto.ArqueoBruto,
                NivelProteccion = dto.NivelProteccion
            };

            // ⚠️ CORRECCIÓN ARQUITECTÓNICA: Filtramos SIEMPRE por IdViaje relacional.
            // Esto evita la creación de documentos fantasmas y mantiene todo consolidado.
            var filtroFinal = Builders<ViajeDetalleMongo>.Filter.Eq(v => v.IdViaje, targetTravelId.Value);

            var update = Builders<ViajeDetalleMongo>.Update
                .Set(x => x.DatosPbip, datosMongo);

            if (costeraId > 0)
            {
                update = update.SetOnInsert(x => x.CosteraIdRaw, (BsonValue)costeraId);
            }

            var updateOptions = new UpdateOptions { IsUpsert = true };
            var result = await _detailsCollection.UpdateOneAsync(filtroFinal, update, updateOptions, cancellationToken: ct);

            // ⚠️ CORRECCIÓN 2: Agregamos result.MatchedCount > 0. Si el usuario le da "Guardar" 
            // sin cambiar ningún dato, ModifiedCount es 0, pero MatchedCount es 1 (Es un éxito).
            return result.ModifiedCount > 0 || result.UpsertedId != null || result.MatchedCount > 0;
        }
    }
}