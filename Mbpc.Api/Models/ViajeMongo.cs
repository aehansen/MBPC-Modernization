using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Mbpc.Api.Models
{
    public class ViajeMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("nombreBuque")]
        public string NombreBuque { get; set; } = null!;

        // Fuerte tipado: Usamos DateTime nativo, no strings
        [BsonElement("fechaInicio")]
        [BsonRepresentation(BsonType.DateTime)] 
        public DateTime FechaInicio { get; set; }
        
        // Podés agregar el resto de las propiedades que necesitemos mapear
        [BsonElement("origen")]
        public string Origen { get; set; } = null!;

        [BsonElement("destino")]
        public string Destino { get; set; } = null!;

        [BsonElement("estado")]
        public string Estado { get; set; } = null!;
    }
}