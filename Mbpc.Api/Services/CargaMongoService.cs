using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mbpc.Api.Services
{
    public class CargaMongoService : ICargaService
    {
        private readonly IMongoCollection<ViajeDetalleMongo> _detailsCollection;
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;

        public CargaMongoService(IMongoClient mongoClient, IOptions<MongoDbSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
            _detailsCollection = database.GetCollection<ViajeDetalleMongo>(settings.Value.DetailsMbpcCollectionName);
            _viajesCollection = database.GetCollection<ViajePosicionMongo>(settings.Value.LastMbpcCollectionName);
        }

        public IEnumerable<CargaDto> ObtenerCargasPorViaje(string parametroBusqueda)
        {
            Console.WriteLine($"\n[DEBUG] --- INICIANDO BÚSQUEDA DE CARGAS ---");
            Console.WriteLine($"[DEBUG] Parámetro recibido desde React: '{parametroBusqueda}'");
            
            string nombreBuque = parametroBusqueda;

            // 1. Verificamos si es un ObjectId nativo de Mongo (24 caracteres)
            if (parametroBusqueda.Length == 24 && MongoDB.Bson.ObjectId.TryParse(parametroBusqueda, out var objectId))
            {
                Console.WriteLine($"[DEBUG] El parámetro es un ObjectId válido. Buscando en last_mbpc...");
                
                // Búsqueda nativa y segura por _id
                var filtroViaje = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
                var viaje = _viajesCollection.Find(filtroViaje).FirstOrDefault();
                
                if (viaje != null)
                {
                    Console.WriteLine($"[DEBUG] Viaje encontrado. Nombre del buque: '{viaje.VesselName}'");
                    if (!string.IsNullOrWhiteSpace(viaje.VesselName))
                    {
                        nombreBuque = viaje.VesselName;
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] ALERTA: No se encontró ningún viaje con el ID {objectId} en last_mbpc.");
                }
            }

            Console.WriteLine($"[DEBUG] Buscando en details_mbpc por VesselName == '{nombreBuque}'...");
            
            // 2. Buscamos TODOS los detalles de ese buque (evita que un registro viejo tape al nuevo)
            var filtroDetalles = Builders<ViajeDetalleMongo>.Filter.Eq("VesselName", nombreBuque);
            var detalles = _detailsCollection.Find(filtroDetalles).ToList();
            
            Console.WriteLine($"[DEBUG] Se encontraron {detalles.Count} documentos en details_mbpc para '{nombreBuque}'.");

            // 3. Buscamos el que tenga barcazas
            var detalleConCargas = detalles.FirstOrDefault(d => d.Barcazas != null && d.Barcazas.Any());

            if (detalleConCargas == null) 
            {
                Console.WriteLine($"[DEBUG] Ninguno de los documentos encontrados tiene el array 'barcazas' lleno.");
                Console.WriteLine($"[DEBUG] --- FIN DE BÚSQUEDA (Vacío) ---\n");
                return new List<CargaDto>();
            }

            Console.WriteLine($"[DEBUG] ¡ÉXITO! Se encontraron {detalleConCargas.Barcazas.Count} barcazas.");
            Console.WriteLine($"[DEBUG] --- FIN DE BÚSQUEDA ---\n");

            return detalleConCargas.Barcazas.Select(b => new CargaDto
            {
                Id = b.Nombre ?? Guid.NewGuid().ToString(), // ID seguro
                ViajeId = nombreBuque,
                DescripcionLista = $"{b.Nombre} - {b.Carga} ({b.Cantidad} {b.Unidad})",
                NivelRiesgo = "Estándar"
            });
        }

        public bool AmarrarBarcaza(string id, string nuevoMuelle) => throw new NotImplementedException();
        public bool FondearBarcaza(string id, string zonaFondeo) => throw new NotImplementedException();
    }
}