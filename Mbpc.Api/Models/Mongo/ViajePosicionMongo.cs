using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Mbpc.Api.Models.Mongo
{
    [BsonIgnoreExtraElements]
    public class ViajePosicionMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("TravelId")]
        public long TravelId { get; set; }

        [BsonElement("VesselName")]
        public string VesselName { get; set; } = null!;

        [BsonElement("MMSI")]
        public string? Mmsi { get; set; }

        [BsonElement("IMO")]
        public int? Imo { get; set; }

        [BsonElement("CallSign")]
        public string? CallSign { get; set; }

        [BsonElement("Latitude")]
        public double Latitude { get; set; }

        [BsonElement("Longitude")]
        public double Longitude { get; set; }

        [BsonElement("NavegationStatusDesc")]
        public string NavegationStatusDesc { get; set; } = null!;

        [BsonElement("SpeedOverGroud")] 
        public double SpeedOverGround { get; set; }

        [BsonElement("CourseOverGround")]
        public double CourseOverGround { get; set; }

        [BsonElement("msgTime")]
        public DateTime MsgTime { get; set; }

        [BsonElement("Origin")]
        public string? Origin { get; set; }

        [BsonElement("Destination")]
        public string? Destination { get; set; }

        [BsonElement("location")]
        public LocationMongo? Location { get; set; }

        // ── MULTITENANT GEOGRÁFICO RESILIENTE ──
        [BsonElement("CosteraId")]
        public object? CosteraIdRaw { get; set; } // Captura Int o String sin explotar

        [BsonIgnore]
        public int? CosteraId 
        { 
            get 
            {
                if (CosteraIdRaw == null) return null;
                if (CosteraIdRaw is int i) return i;
                if (CosteraIdRaw is long l) return (int)l;
                // Si es un string (ej: "RÍO PARANÁ..."), intentamos parsear o devolvemos null
                if (int.TryParse(CosteraIdRaw.ToString(), out var result)) return result;
                return null; 
            }
            set => CosteraIdRaw = value;
        }
    }

    public class LocationMongo
    {
        [BsonElement("geo")]
        public GeoMongo Geo { get; set; } = null!;
    }

    public class GeoMongo
    {
        [BsonElement("type")]
        public string Type { get; set; } = null!;

        [BsonElement("coordinates")]
        public double[] Coordinates { get; set; } = null!;
    }
}