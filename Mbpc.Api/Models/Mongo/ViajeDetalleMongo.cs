// Mbpc.Api/Models/Mongo/ViajeDetalleMongo.cs

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

        // ─── TÉCNICA DE PROPIEDADES DE RESPALDO (Tolerancia BSON) ───
        [BsonElement("ETAPAS")] public List<EtapaMongo>? EtapasLegacy { get; set; }
        [BsonElement("etapas")] public List<EtapaMongo>? EtapasModern { get; set; }
        
        [BsonIgnore] 
        public List<EtapaMongo> Etapas 
        { 
            get 
            {
                if (EtapasModern != null) return EtapasModern;
                if (EtapasLegacy != null) return EtapasLegacy;
                // Inicialización perezosa segura para no agregar a una lista "huerfana"
                EtapasModern = new List<EtapaMongo>();
                return EtapasModern;
            }
            set => EtapasModern = value; 
        }

        // PROPIEDAD DE RESPALDO LEGACY RAÍZ
        // Captura las barcazas que aún existan en la raíz del documento (pre-CQRS)
        [BsonElement("BARCAZAS")] public List<BarcazaMongo>? BarcazasRootLegacy { get; set; }
        [BsonElement("barcazas")] public List<BarcazaMongo>? BarcazasRootModern { get; set; }
        
        [BsonIgnore]
        public List<BarcazaMongo>? BarcazasLegacy
        {
            get => BarcazasRootModern ?? BarcazasRootLegacy;
            set => BarcazasRootModern = value;
        }

        [BsonElement("CosteraId")]
        public int? CosteraId { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class EtapaMongo
    {
        [BsonElement("ETAPA_ID")] public long? EtapaIdLegacy { get; set; }
        [BsonElement("etapaId")]  public long? EtapaIdModern { get; set; }
        [BsonElement("EtapaId")]  public long? EtapaIdPascal { get; set; }
        [BsonIgnore] public long EtapaId { get => EtapaIdPascal ?? EtapaIdModern ?? EtapaIdLegacy ?? 0; set => EtapaIdPascal = value; }

        [BsonElement("FECHA_INICIO")] public DateTime? FechaInicioLegacy { get; set; }
        [BsonElement("fechaInicio")]  public DateTime? FechaInicioModern { get; set; }
        [BsonElement("FechaInicio")]  public DateTime? FechaInicioPascal { get; set; }
        [BsonIgnore] public DateTime? FechaInicio { get => FechaInicioPascal ?? FechaInicioModern ?? FechaInicioLegacy; set => FechaInicioPascal = value; }

        [BsonElement("REMOLCADOR")] public RemolcadorMongo? RemolcadorLegacy { get; set; }
        [BsonElement("remolcador")] public RemolcadorMongo? RemolcadorModern { get; set; }
        [BsonIgnore] public RemolcadorMongo? Remolcador { get => RemolcadorModern ?? RemolcadorLegacy; set => RemolcadorModern = value; }

        [BsonElement("BARCAZAS")] public List<BarcazaMongo>? BarcazasLegacy { get; set; }
        [BsonElement("barcazas")] public List<BarcazaMongo>? BarcazasModern { get; set; }
        [BsonIgnore] public List<BarcazaMongo>? Barcazas { get => BarcazasModern ?? BarcazasLegacy; set => BarcazasModern = value; }
    }

    [BsonIgnoreExtraElements]
    public class RemolcadorMongo
    {
        [BsonElement("NOMBRE")] public string? NombreLegacy { get; set; }
        [BsonElement("nombre")] public string? NombreModern { get; set; }
        [BsonIgnore] public string? Nombre { get => NombreModern ?? NombreLegacy; set => NombreModern = value; }

        [BsonElement("MATRICULA")] public string? MatriculaLegacy { get; set; }
        [BsonElement("matricula")] public string? MatriculaModern { get; set; }
        [BsonIgnore] public string? Matricula { get => MatriculaModern ?? MatriculaLegacy; set => MatriculaModern = value; }
    }

    [BsonIgnoreExtraElements]
    public class BarcazaMongo
    {
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