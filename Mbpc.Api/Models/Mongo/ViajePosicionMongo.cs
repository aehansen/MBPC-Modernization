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
        public string Id { get; set; }

        [BsonElement("TravelId")]
        public long TravelId { get; set; }

        [BsonElement("VesselName")]
        public string VesselName { get; set; }

        // Puede ser nulo en algunos registros (ej: ANGELITA B)
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
        public string NavegationStatusDesc { get; set; }

        [BsonElement("SpeedOverGroud")] // Respetamos el typo original de la base ("Groud")
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
    }

    public class LocationMongo
    {
        [BsonElement("geo")]
        public GeoMongo Geo { get; set; }
    }

    public class GeoMongo
    {
        [BsonElement("type")]
        public string Type { get; set; }

        // MongoDB GeoJSON: [Longitud, Latitud]
        [BsonElement("coordinates")]
        public double[] Coordinates { get; set; }
    }
}
