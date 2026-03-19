using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Mbpc.Api.Models;
using Mbpc.Api.DTOs;
using Mbpc.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Mbpc.Api.Services
{
    public class CargaMongoService : ICargaService //
    {
        private readonly IMongoCollection<CargaMongo> _cargasCollection;

        public CargaMongoService(IMongoClient mongoClient, IOptions<MongoDbSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
            _cargasCollection = database.GetCollection<CargaMongo>("cargas");
        }

        // CORRECCIÓN: Ahora recibe string viajeId
        public IEnumerable<CargaDto> ObtenerCargasPorViaje(string viajeId)
        {
            var filtro = Builders<CargaMongo>.Filter.Eq(c => c.ViajeId, viajeId);
            var cargasMongo = _cargasCollection.Find(filtro).ToList();

            return cargasMongo.Select(c => new CargaDto
            {
                Id = c.Id, // Mapeo directo de string a string
                ViajeId = c.ViajeId,
                DescripcionLista = $"{c.TipoMercaderia} ({c.Toneladas} tons.)",
                NivelRiesgo = c.EsPeligrosa ? "Alto" : "Estándar"
            });
        }

        // CORRECCIÓN: Ahora recibe string cargaId
        public bool AmarrarBarcaza(string cargaId, string nuevoMuelle)
        {
            throw new NotImplementedException("Modernización: La actualización de estados se delegará a Oracle.");
        }

        // CORRECCIÓN: Ahora recibe string cargaId
        public bool FondearBarcaza(string cargaId, string zonaFondeo)
        {
            throw new NotImplementedException("Modernización: La actualización de estados se delegará a Oracle.");
        }
    }
}