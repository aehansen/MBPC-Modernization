using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mbpc.Api.Models
{
    public class CargaMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        // Usamos int temporalmente para cumplir con la interfaz actual
        [BsonElement("viajeId")]
        [BsonRepresentation(BsonType.ObjectId)] // Le decimos que es un ObjectId guardado como string
        public string ViajeId { get; set; } = null!;

        [BsonElement("tipoMercaderia")]
        public string TipoMercaderia { get; set; } = null!;

        [BsonElement("toneladas")]
        public double Toneladas { get; set; }

        [BsonElement("esPeligrosa")]
        public bool EsPeligrosa { get; set; }
    }
}