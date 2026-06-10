using Dapper;
using Oracle.ManagedDataAccess.Client;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Options;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services.Auth;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mbpc.Api.Services
{
    public class InspeccionManagerService : IInspeccionService
    {
        private readonly IMongoCollection<InspeccionMongo> _inspeccionesCollection;
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        private readonly string _oracleConnectionString;
        private readonly ICosteraUserContext _costeraUserContext;
        private readonly ILogger<InspeccionManagerService> _logger;
        private readonly IWebHostEnvironment _env;

        public InspeccionManagerService(
            IMongoClient mongoClient,
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings,
            ICosteraUserContext costeraUserContext,
            ILogger<InspeccionManagerService> logger,
            IWebHostEnvironment env)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _inspeccionesCollection = database.GetCollection<InspeccionMongo>("inspecciones_mbpc");
            _viajesCollection = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _costeraUserContext = costeraUserContext;
            _logger = logger;
            _env = env;
        }

        private FilterDefinition<InspeccionMongo> BuildFiltroCostera(int costeraId)
        {
            return costeraId == 0
                ? Builders<InspeccionMongo>.Filter.Empty
                : Builders<InspeccionMongo>.Filter.Eq(i => i.CosteraId, costeraId);
        }

        private async Task<long> ResolveTravelIdAndBuqueIdAsync(string viajeId, Action<int> setBuqueId)
        {
            long travelId = 0;
            int buqueId = 0;

            // 1. Intentamos buscar el viaje en MongoDB para obtener su TravelId numérico
            if (ObjectId.TryParse(viajeId, out var objId))
            {
                var viajeMongo = await _viajesCollection.Find(v => v.Id == viajeId).FirstOrDefaultAsync();
                if (viajeMongo != null)
                {
                    travelId = viajeMongo.TravelId;
                }
            }
            else
            {
                long.TryParse(viajeId, out travelId);
            }

            // 2. Teniendo el TravelId, consultamos el BUQUE_ID correspondiente de Oracle
            if (travelId > 0)
            {
                try
                {
                    using var connection = new OracleConnection(_oracleConnectionString);
                    buqueId = await connection.ExecuteScalarAsync<int>(
                        "SELECT BUQUE_ID FROM MBPC.TBL_VIAJE WHERE ID = :TravelId", new { TravelId = travelId });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo recuperar el BUQUE_ID desde Oracle para el TravelId {TravelId}. Usando fallback.", travelId);
                }
            }

            setBuqueId(buqueId);
            return travelId;
        }

        public async Task<IEnumerable<InspeccionDto>> ObtenerInspeccionesAsync(string? viajeId = null, int pagina = 1, int tamanio = 50)
        {
            int costeraId = _costeraUserContext.GetCurrentCosteraId();
            _logger.LogInformation("Consultando inspecciones - CosteraId: {CosteraId}, ViajeId: {ViajeId}", costeraId, viajeId);

            var filter = BuildFiltroCostera(costeraId);

            if (!string.IsNullOrWhiteSpace(viajeId))
            {
                filter = Builders<InspeccionMongo>.Filter.And(filter, Builders<InspeccionMongo>.Filter.Eq(i => i.ViajeId, viajeId));
            }

            var skip = (pagina - 1) * tamanio;
            var list = await _inspeccionesCollection.Find(filter)
                .Skip(skip)
                .Limit(tamanio)
                .ToListAsync();

            return list.Select(i => new InspeccionDto
            {
                Id = i.Id,
                ViajeId = i.ViajeId,
                BuqueId = i.BuqueId,
                FechaInspeccion = i.FechaInspeccion,
                TipoInspeccion = i.TipoInspeccion,
                Resultado = i.Resultado,
                Observaciones = i.Observaciones,
                InspectorDatos = i.InspectorDatos,
                LugarInspeccion = i.LugarInspeccion,
                CosteraId = i.CosteraId
            });
        }

        public async Task<InspeccionDto?> ObtenerPorIdAsync(Guid id)
        {
            int costeraId = _costeraUserContext.GetCurrentCosteraId();
            var filter = Builders<InspeccionMongo>.Filter.And(
                Builders<InspeccionMongo>.Filter.Eq(i => i.Id, id),
                BuildFiltroCostera(costeraId)
            );

            var doc = await _inspeccionesCollection.Find(filter).FirstOrDefaultAsync();
            if (doc == null) return null;

            return new InspeccionDto
            {
                Id = doc.Id,
                ViajeId = doc.ViajeId,
                BuqueId = doc.BuqueId,
                FechaInspeccion = doc.FechaInspeccion,
                TipoInspeccion = doc.TipoInspeccion,
                Resultado = doc.Resultado,
                Observaciones = doc.Observaciones,
                InspectorDatos = doc.InspectorDatos,
                LugarInspeccion = doc.LugarInspeccion,
                CosteraId = doc.CosteraId
            };
        }

        public async Task<bool> CrearInspeccionAsync(CrearInspeccionDto dto)
        {
            int costeraId = _costeraUserContext.GetCurrentCosteraId();
            dto.CosteraId = costeraId;

            _logger.LogInformation("Creando inspección para ViajeId: {ViajeId}, CosteraId: {CosteraId}", dto.ViajeId, dto.CosteraId);

            var idGenerado = Guid.NewGuid();
            bool exitoOracle = false;

            int buqueId = 0;
            long travelId = await ResolveTravelIdAndBuqueIdAsync(dto.ViajeId, (bId) => { buqueId = bId; dto.BuqueId = bId; });

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID", idGenerado.ToString());
                parameters.Add("p_VIAJE_ID", travelId);
                parameters.Add("p_BUQUE_ID", buqueId);
                parameters.Add("p_FECHA", dto.FechaInspeccion);
                parameters.Add("p_TIPO_INSPECCION", dto.TipoInspeccion);
                parameters.Add("p_RESULTADO", dto.Resultado);
                parameters.Add("p_OBSERVACIONES", dto.Observaciones);
                parameters.Add("p_INSPECTOR_DATOS", dto.InspectorDatos);
                parameters.Add("p_LUGAR", dto.LugarInspeccion);
                parameters.Add("p_COSTERA_ID", dto.CosteraId);
                parameters.Add("p_RESULTADO_OUT", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "PKG_MBPC_INSPECCIONES.SP_CREAR_INSPECCION", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO_OUT") == 1;
            }
            catch (OracleException ex)
            {
                _logger.LogWarning(ex, "Bypass Activo: Error en base de datos Oracle al crear inspección. Procediendo a registrar en MongoDB.");
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    var mongoDoc = new InspeccionMongo
                    {
                        Id = idGenerado,
                        ViajeId = dto.ViajeId,
                        BuqueId = dto.BuqueId,
                        FechaInspeccion = dto.FechaInspeccion,
                        TipoInspeccion = dto.TipoInspeccion,
                        Resultado = dto.Resultado,
                        Observaciones = dto.Observaciones,
                        InspectorDatos = dto.InspectorDatos,
                        LugarInspeccion = dto.LugarInspeccion,
                        CosteraId = dto.CosteraId
                    };
                    await _inspeccionesCollection.InsertOneAsync(mongoDoc);
                    return true;
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Error al replicar la creación de inspección en MongoDB.");
                    return false;
                }
            }

            return false;
        }

        public async Task<bool> ModificarInspeccionAsync(Guid id, ModificarInspeccionDto dto)
        {
            int costeraId = _costeraUserContext.GetCurrentCosteraId();
            _logger.LogInformation("Modificando inspección {Id} para ViajeId: {ViajeId}, CosteraId: {CosteraId}", id, dto.ViajeId, costeraId);

            bool exitoOracle = false;
            int buqueId = 0;
            long travelId = await ResolveTravelIdAndBuqueIdAsync(dto.ViajeId, (bId) => { buqueId = bId; dto.BuqueId = bId; });

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID", id.ToString());
                parameters.Add("p_VIAJE_ID", travelId);
                parameters.Add("p_BUQUE_ID", buqueId);
                parameters.Add("p_FECHA", dto.FechaInspeccion);
                parameters.Add("p_TIPO_INSPECCION", dto.TipoInspeccion);
                parameters.Add("p_RESULTADO", dto.Resultado);
                parameters.Add("p_OBSERVACIONES", dto.Observaciones);
                parameters.Add("p_INSPECTOR_DATOS", dto.InspectorDatos);
                parameters.Add("p_LUGAR", dto.LugarInspeccion);
                parameters.Add("p_RESULTADO_OUT", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "PKG_MBPC_INSPECCIONES.SP_MODIFICAR_INSPECCION", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO_OUT") == 1;
            }
            catch (OracleException ex)
            {
                _logger.LogWarning(ex, "Bypass Activo: Error en base de datos Oracle al modificar inspección. Procediendo a registrar en MongoDB.");
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    var filter = Builders<InspeccionMongo>.Filter.And(
                        Builders<InspeccionMongo>.Filter.Eq(i => i.Id, id),
                        Builders<InspeccionMongo>.Filter.Eq(i => i.ViajeId, dto.ViajeId),
                        BuildFiltroCostera(costeraId)
                    );

                    var update = Builders<InspeccionMongo>.Update
                        .Set(i => i.BuqueId, dto.BuqueId)
                        .Set(i => i.FechaInspeccion, dto.FechaInspeccion)
                        .Set(i => i.TipoInspeccion, dto.TipoInspeccion)
                        .Set(i => i.Resultado, dto.Resultado)
                        .Set(i => i.Observaciones, dto.Observaciones)
                        .Set(i => i.InspectorDatos, dto.InspectorDatos)
                        .Set(i => i.LugarInspeccion, dto.LugarInspeccion);

                    var result = await _inspeccionesCollection.UpdateOneAsync(filter, update);
                    return result.ModifiedCount > 0;
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Error al replicar modificación de inspección en MongoDB.");
                    return false;
                }
            }

            return false;
        }

        public async Task<bool> EliminarInspeccionAsync(Guid id, string viajeId)
        {
            int costeraId = _costeraUserContext.GetCurrentCosteraId();
            _logger.LogInformation("Eliminando inspección {Id} con ViajeId {ViajeId} y CosteraId {CosteraId}", id, viajeId, costeraId);

            bool exitoOracle = false;
            long.TryParse(viajeId, out long travelId);

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID", id.ToString());
                parameters.Add("p_VIAJE_ID", travelId);
                parameters.Add("p_RESULTADO_OUT", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "PKG_MBPC_INSPECCIONES.SP_ELIMINAR_INSPECCION", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO_OUT") == 1;
            }
            catch (OracleException ex)
            {
                _logger.LogWarning(ex, "Bypass Activo: Error en base de datos Oracle al eliminar inspección. Procediendo a registrar en MongoDB.");
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    var filter = Builders<InspeccionMongo>.Filter.And(
                        Builders<InspeccionMongo>.Filter.Eq(i => i.Id, id),
                        Builders<InspeccionMongo>.Filter.Eq(i => i.ViajeId, viajeId),
                        BuildFiltroCostera(costeraId)
                    );

                    var result = await _inspeccionesCollection.DeleteOneAsync(filter);
                    return result.DeletedCount > 0;
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Error al replicar eliminación de inspección en MongoDB.");
                    return false;
                }
            }

            return false;
        }
    }
}
