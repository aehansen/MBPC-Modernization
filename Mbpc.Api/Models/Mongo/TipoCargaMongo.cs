using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mbpc.Api.Models.Mongo
{
    [BsonIgnoreExtraElements]
    public class TipoCargaMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("OracleId")]
        public int OracleId { get; set; }

        [BsonElement("Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [BsonElement("Codigo")]
        public string Codigo { get; set; } = string.Empty;

        [BsonElement("EsPeligrosa")]
        public bool EsPeligrosa { get; set; }
    }
}