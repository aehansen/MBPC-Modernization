using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace Mbpc.Api.Models.Mongo
{
    [BsonIgnoreExtraElements]
    public class CosteraMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("costeraId")]
        public int CosteraId { get; set; }

        [BsonElement("nombre")]
        public string Nombre { get; set; } = null!;

        [BsonElement("geometria")]
        public GeoJsonGeometry<GeoJson2DGeographicCoordinates> Geometria { get; set; } = null!;
    }
}
