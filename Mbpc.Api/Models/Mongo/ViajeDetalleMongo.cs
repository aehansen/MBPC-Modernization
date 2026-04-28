using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Mbpc.Api.Models.Mongo
{
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

        [BsonElement("etapas")]
        public List<EtapaMongo> Etapas { get; set; } = new();

        // PROPIEDAD DE RESPALDO LEGACY
        // Captura las barcazas que aún existan en la raíz del documento
        [BsonElement("barcazas")]
        [BsonIgnoreIfNull]
        public List<BarcazaMongo>? BarcazasLegacy { get; set; }

        [BsonElement("CosteraId")]
        public int? CosteraId { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class EtapaMongo
    {
        [BsonElement("EtapaId")]
        public long EtapaId { get; set; }

        [BsonElement("FechaInicio")]
        public DateTime? FechaInicio { get; set; }

        [BsonElement("remolcador")]
        public RemolcadorMongo? Remolcador { get; set; }

        [BsonElement("barcazas")]
        public List<BarcazaMongo>? Barcazas { get; set; }
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
        // ─── TÉCNICA DE PROPIEDADES DE RESPALDO (Tolerancia BSON) ───
        
        [BsonElement("ID_VIAJE")] public long? IdViajeLegacy { get; set; }
        [BsonElement("idViaje")]  public long? IdViajeModern { get; set; }
        [BsonIgnore] public long IdViaje { get => IdViajeModern ?? IdViajeLegacy ?? 0; set => IdViajeModern = value; }

        [BsonElement("BARCAZA")] public string? NombreLegacy { get; set; }
        [BsonElement("nombre")]  public string? NombreModern { get; set; }
        [BsonIgnore] public string Nombre { get => NombreModern ?? NombreLegacy ?? string.Empty; set => NombreModern = value; }

        [BsonElement("BANDERA")] public string? BanderaLegacy { get; set; }
        [BsonElement("bandera")] public string? BanderaModern { get; set; }
        [BsonIgnore] public string Bandera { get => BanderaModern ?? BanderaLegacy ?? string.Empty; set => BanderaModern = value; }

        [BsonElement("MATRICULA")] public string? MatriculaLegacy { get; set; }
        [BsonElement("matricula")] public string? MatriculaModern { get; set; }
        [BsonIgnore] public string? Matricula { get => MatriculaModern ?? MatriculaLegacy; set => MatriculaModern = value; }

        [BsonElement("CARGA")] public string? CargaLegacy { get; set; }
        [BsonElement("carga")] public string? CargaModern { get; set; }
        [BsonIgnore] public string Carga { get => CargaModern ?? CargaLegacy ?? string.Empty; set => CargaModern = value; }

        [BsonElement("CANTIDAD")] public double? CantidadLegacy { get; set; }
        [BsonElement("cantidad")] public double? CantidadModern { get; set; }
        [BsonIgnore] public double Cantidad { get => CantidadModern ?? CantidadLegacy ?? 0; set => CantidadModern = value; }

        [BsonElement("UNIDAD")] public string? UnidadLegacy { get; set; }
        [BsonElement("unidad")] public string? UnidadModern { get; set; }
        [BsonIgnore] public string Unidad { get => UnidadModern ?? UnidadLegacy ?? string.Empty; set => UnidadModern = value; }

        [BsonElement("MUELLE_ACTUAL")] public string? MuelleActualLegacy { get; set; }
        [BsonElement("muelleActual")]  public string? MuelleActualModern { get; set; }
        [BsonIgnore] public string? MuelleActual { get => MuelleActualModern ?? MuelleActualLegacy; set => MuelleActualModern = value; }

        [BsonElement("MERCADERIA_ID")] public int? MercaderiaIdLegacy { get; set; }
        [BsonElement("mercaderiaId")]  public int? MercaderiaIdModern { get; set; }
        [BsonIgnore] public int? MercaderiaId { get => MercaderiaIdModern ?? MercaderiaIdLegacy; set => MercaderiaIdModern = value; }
    }
}