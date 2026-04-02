using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Mbpc.Api.Models.Mongo
{
    /// <summary>
    /// Documento de detalle operativo de un viaje almacenado en MongoDB.
    /// Complementa a ViajePosicionMongo con datos de negocio provenientes de Oracle
    /// (barcazas, remolcador, etc.) que se sincronizan vía CQRS.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class ViajeDetalleMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("IdViaje")]
        public long IdViaje { get; set; }

        [BsonElement("VesselName")]
        public string? VesselName { get; set; }

        [BsonElement("Origin")]
        public string? Origin { get; set; }

        [BsonElement("Destination")]
        public string? Destination { get; set; }

        [BsonElement("Remolcador")]
        public RemolcadorMongo? Remolcador { get; set; }

        [BsonElement("Barcazas")]
        public List<BarcazaMongo>? Barcazas { get; set; }

        // ── MULTITENANT GEOGRÁFICO ──
        // El campo CosteraId en BSON es numérico (Int32).
        // CosteraId == 0  →  registro global / Super Admin.
        // CosteraId  > 0  →  registro restringido a esa jurisdicción costera.
        [BsonElement("CosteraId")]
        public int? CosteraId { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class RemolcadorMongo
    {
        [BsonElement("Nombre")]
        public string? Nombre { get; set; }

        [BsonElement("Matricula")]
        public string? Matricula { get; set; }
    }

    [BsonIgnoreExtraElements]
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

        [BsonElement("MUELLE_ACTUAL")]
        [BsonIgnoreIfNull]
        public string? MuelleActual { get; set; }
    }
}