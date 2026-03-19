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
    public class ViajeMongoService : IViajeService
    {
        private readonly IMongoCollection<ViajeMongo> _viajesCollection;

        // Inyectamos el cliente de Mongo y las opciones tipadas (Nada de configuraciones harcodeadas)
        public ViajeMongoService(IMongoClient mongoClient, IOptions<MongoDbSettings> settings)
        {
            var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
            // Apuntamos a la colección donde caen los datos sincronizados
            _viajesCollection = database.GetCollection<ViajeMongo>("viajes"); 
        }

        public IEnumerable<ViajeDto> ObtenerViajesActivos()
        {
            // Lectura real desde MongoDB: Filtramos los viajes activos
            var filtro = Builders<ViajeMongo>.Filter.In(v => v.Estado, new[] { "En Curso", "Fondeado" });
            
            var viajesMongo = _viajesCollection.Find(filtro).ToList();

            // Mapeamos al DTO fuertemente tipado para el frontend
            return viajesMongo.Select(MapearADto);
        }

        public ViajeDto CrearViaje(NuevoViajeDto nuevoViaje)
        {
            // REGLA DE ARQUITECTURA: La escritura va a Oracle. 
            // Por ahora, dejamos este método preparado para cuando armemos el repositorio de Oracle.
            // Para no romper la interfaz IViajeService, lanzamos una excepción clara.
            throw new NotImplementedException("Modernización en curso: La escritura de nuevos viajes se delegará a la capa de Oracle.");
        }

        // Método auxiliar privado para mantener el desacoplamiento entre el modelo de BD y el DTO
        private ViajeDto MapearADto(ViajeMongo v)
        {
            return new ViajeDto
            {
                // Como Mongo usa ObjectId (string) y nuestro DTO viejo usaba int, 
                // usamos el GetHashCode temporalmente para el prototipo en React, 
                // o idealmente refactorizamos ViajeDto para soportar strings.
                Id = v.Id!, 
                Buque = v.NombreBuque,
                Ruta = $"{v.Origen} -> {v.Destino}",
                FechaInicioFormateada = v.FechaInicio.ToString("dd/MM/yyyy HH:mm"),
                EstadoActual = v.Estado
            };
        }
    }
}