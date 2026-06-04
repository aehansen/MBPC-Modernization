using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mbpc.Api.Models.Mongo
{
    [BsonIgnoreExtraElements]
    public class EventoViajeMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("tipoEvento")]
        [BsonRepresentation(BsonType.String)]
        public TipoEventoViaje TipoEvento { get; set; }

        [BsonElement("fechaHora")]
        public DateTime FechaHora { get; set; } = DateTime.UtcNow;

        [BsonElement("usuario")]
        public string Usuario { get; set; } = "Sistema";

        [BsonElement("detalle")]
        public string Detalle { get; set; } = string.Empty;

        [BsonElement("estadoAnterior")]
        [BsonIgnoreIfNull]
        public string? EstadoAnterior { get; set; }

        [BsonElement("estadoNuevo")]
        [BsonIgnoreIfNull]
        public string? EstadoNuevo { get; set; }
    }
}
