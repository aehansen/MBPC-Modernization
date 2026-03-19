using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Mbpc.Api.Models.Mongo
{
    [BsonIgnoreExtraElements] // <-- BLINDAJE CONTRA CAMPOS EXTRA COMO 'IMO'
    public class ViajeDetalleMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("VesselName")]
        public string VesselName { get; set; } = null!;

        [BsonElement("Origin")]
        public string? Origin { get; set; }

        [BsonElement("Destination")]
        public string? Destination { get; set; }

        [BsonElement("barcazas")]
        public List<BarcazaMongo>? Barcazas { get; set; }

        [BsonElement("remolcador")]
        public RemolcadorMongo? Remolcador { get; set; }
    }

    [BsonIgnoreExtraElements] // <-- BLINDAJE PARA LAS BARCAZAS
    public class BarcazaMongo
    {
        [BsonElement("ID_VIAJE")]
        public long IdViaje { get; set; }

        [BsonElement("BARCAZA")]
        public string Nombre { get; set; } = null!;

        [BsonElement("BANDERA")]
        public string Bandera { get; set; } = null!;

        [BsonElement("MATRICULA")]
        public string? Matricula { get; set; }

        [BsonElement("CARGA")]
        public string Carga { get; set; } = null!;

        [BsonElement("CANTIDAD")]
        public double Cantidad { get; set; }

        [BsonElement("UNIDAD")]
        public string Unidad { get; set; } = null!;
    }

    [BsonIgnoreExtraElements] // <-- BLINDAJE PARA EL REMOLCADOR
    public class RemolcadorMongo
    {
        [BsonElement("ID_VIAJE")]
        public long IdViaje { get; set; }

        [BsonElement("REMOLCADOR")]
        public string Nombre { get; set; } = null!;

        [BsonElement("ESTADO")]
        public string Estado { get; set; } = null!;

        [BsonElement("FECHA_SALIDA")]
        public string? FechaSalida { get; set; } 
    }
}