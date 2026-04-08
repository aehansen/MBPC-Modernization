using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mbpc.Api.Models.Mongo
{
    /// <summary>
    /// Registro inmutable en la colección "tracklog_mbpc".
    /// Cada actualización de posición inserta un nuevo documento aquí;
    /// nunca se modifica. Permite reconstruir la trayectoria completa del buque.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class ViajeTracklogMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        /// <summary>ObjectId del documento padre en "posiciones_mbpc".</summary>
        [BsonElement("PosicionId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string PosicionId { get; set; } = null!;

        [BsonElement("TravelId")]
        public long TravelId { get; set; }

        [BsonElement("VesselName")]
        public string VesselName { get; set; } = null!;

        [BsonElement("MMSI")]
        public string? Mmsi { get; set; }

        [BsonElement("Latitude")]
        public double Latitude { get; set; }

        [BsonElement("Longitude")]
        public double Longitude { get; set; }

        [BsonElement("SpeedOverGroud")]   // Typo intencional: consistencia con colección origen
        public double SpeedOverGround { get; set; }

        /// <summary>Velocidad calculada en nudos entre la posición anterior y esta.</summary>
        [BsonElement("CalculatedSpeedKnots")]
        public double CalculatedSpeedKnots { get; set; }

        /// <summary>Distancia en millas náuticas desde la posición anterior.</summary>
        [BsonElement("DistanceNM")]
        public double DistanceNM { get; set; }

        [BsonElement("NavegationStatusDesc")]
        public string NavegationStatusDesc { get; set; } = null!;

        /// <summary>Timestamp del mensaje AIS reportado por el transponder.</summary>
        [BsonElement("msgTime")]
        public DateTime MsgTime { get; set; }

        /// <summary>Timestamp de inserción en servidor (UTC). Inmutable.</summary>
        [BsonElement("insertedAt")]
        public DateTime InsertedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("CosteraId")]
        public int? CosteraId { get; set; }

        [BsonElement("location")]
        public LocationMongo? Location { get; set; }
    }
}
