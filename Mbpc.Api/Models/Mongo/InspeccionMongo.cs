using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Mbpc.Api.Models.Mongo
{
    [BsonIgnoreExtraElements]
    public class InspeccionMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }

        [BsonElement("ViajeId")]
        public string ViajeId { get; set; } = string.Empty;

        [BsonElement("BuqueId")]
        public int BuqueId { get; set; }

        [BsonElement("FechaInspeccion")]
        public DateTime FechaInspeccion { get; set; }

        [BsonElement("TipoInspeccion")]
        public string TipoInspeccion { get; set; } = string.Empty;

        [BsonElement("Resultado")]
        public string Resultado { get; set; } = string.Empty;

        [BsonElement("Observaciones")]
        public string Observaciones { get; set; } = string.Empty;

        [BsonElement("InspectorDatos")]
        public string InspectorDatos { get; set; } = string.Empty;

        [BsonElement("LugarInspeccion")]
        public string LugarInspeccion { get; set; } = string.Empty;

        [BsonElement("CosteraId")]
        public int CosteraId { get; set; }
    }
}
