using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mbpc.Api.Services
{
    public class ViajeMongoService : IViajeService
    {
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;

        // Inyectamos el cliente de Mongo y nuestra configuración fuertemente tipada
        public ViajeMongoService(IMongoClient mongoClient, IOptions<MongoDbSettings> mongoDbSettings)
        {
            var database = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
            
            // Nos conectamos a la colección 'last_mbpc' mapeándola a nuestro DTO
            _viajesCollection = database.GetCollection<ViajePosicionMongo>(mongoDbSettings.Value.LastMbpcCollectionName);
        }

        public async Task<List<ViajePosicionMongo>> GetViajesAsync()
        {
            // Traemos todos los registros. 
            // En un futuro cercano le agregaremos paginación o filtros por RCK/Cuadrante.
            return await _viajesCollection.Find(_ => true).ToListAsync();
        }

        public async Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi)
        {
            // Búsqueda específica usando el MMSI, ideal para cuando hagamos el tracking de un buque
            return await _viajesCollection.Find(v => v.Mmsi == mmsi).FirstOrDefaultAsync();
        }

        // TODO: Métodos de escritura (POST/PUT) que luego conectaremos con Oracle 
        // para mantener la sincronización del patrón Strangler Fig.
    }
}