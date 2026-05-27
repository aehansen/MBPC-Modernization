// Mbpc.Api/Models/Mongo/ViajeDetalleMongo.cs

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Mbpc.Api.Models.Mongo
{
    // ─────────────────────────────────────────────────────────────────────────────
    // ESTRATEGIA DE MAPPING:
    //
    //  • [BsonIgnoreExtraElements] → silencia campos legacy (MAYÚSCULAS, Pascal, etc.)
    //    mientras se migra la base. No rompe si existen, simplemente los ignora.
    //
    //  • [BsonElement("camelCase")] → define el nombre canónico de escritura (Modern).
    //
    //  • [BsonIgnoreIfNull] → nunca persiste un campo null en MongoDB.
    //    Evita que documentos nuevos queden llenos de basura.
    //
    //  • CosteraId usa [BsonRepresentation] + lógica de conversión en getter
    //    para tolerar int, long o string provenientes de documentos sucios.
    //
    //  • Propiedades [BsonIgnore] → lógica de negocio pura, nunca se serializan.
    // ─────────────────────────────────────────────────────────────────────────────

    [BsonIgnoreExtraElements]
    public class ViajeDetalleMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("IdViaje")]
        [BsonIgnoreIfNull]
        public long? IdViaje { get; set; }

        [BsonElement("VesselName")]
        [BsonIgnoreIfNull]
        public string? VesselName { get; set; }

        [BsonElement("Origin")]
        [BsonIgnoreIfNull]
        public string? Origin { get; set; }

        [BsonElement("Destination")]
        [BsonIgnoreIfNull]
        public string? Destination { get; set; }

        [BsonElement("ETAPAS")]
        [BsonIgnoreIfNull]
        public List<EtapaMongo>? Etapas { get; set; }

        // FIX Hito 6.0: el sistema legacy persiste las barcazas en un array raíz
        // con clave en minúscula ("barcazas"). Se actualiza el BsonElement para
        // que el driver pueda deserializar correctamente documentos legacy.
        // La propiedad Barcazas dentro de EtapaMongo ("BARCAZAS") se mantiene
        // intacta para convivencia con el formato nuevo.
        [BsonElement("barcazas")]
        [BsonIgnoreIfNull]
        public List<BarcazaMongo>? Barcazas { get; set; }

        // FIX Hito 6.0: el sistema legacy persiste el remolcador en un objeto
        // raíz con clave en minúscula ("remolcador"), fuera del array de etapas.
        // Se agrega esta propiedad como fallback de lectura. Nunca reemplaza la
        // fuente canónica (Etapas[last].Remolcador), solo se usa si esta es nula.
        [BsonElement("remolcador")]
        [BsonIgnoreIfNull]
        public RemolcadorMongo? RemolcadorLegacy { get; set; }

        // ── MULTITENANT GEOGRÁFICO RESILIENTE ────────────────────────────────────
        // Se almacena como BsonValue para tolerar int, long o string en documentos
        // sucios. El getter expuesto a negocio siempre devuelve int?.
        [BsonElement("CosteraId")]
        [BsonIgnoreIfNull]
        public BsonValue? CosteraIdRaw { get; set; }

        [BsonIgnore]
        public int? CosteraId
        {
            get
            {
                if (CosteraIdRaw == null || CosteraIdRaw.IsBsonNull) return null;
                if (CosteraIdRaw.IsInt32) return CosteraIdRaw.AsInt32;
                if (CosteraIdRaw.IsInt64) return (int)CosteraIdRaw.AsInt64;
                if (int.TryParse(CosteraIdRaw.ToString(), out var result)) return result;
                return null;
            }
            set => CosteraIdRaw = value.HasValue ? (BsonValue)value.Value : BsonNull.Value;
        }

        [BsonElement("pbip")]
        [BsonIgnoreIfNull]
        public PbipMongo? Pbip { get; set; }

        [BsonElement("inspectores")]
        [BsonIgnoreIfNull]
        public List<InspectorMongo>? Inspectores { get; set; }

        [BsonElement("practicos")]
        [BsonIgnoreIfNull]
        public List<PracticoMongo>? Practicos { get; set; }

        [BsonElement("NotasBitacora")]
        [BsonIgnoreIfNull]
        public List<NotaBitacoraMongo>? NotasBitacora { get; set; }

        [BsonElement("Agencias")]
        [BsonIgnoreIfNull]
        public List<AgenciaMongo>? Agencias { get; set; }

        [BsonElement("DatosPbip")]
        [BsonIgnoreIfNull]
        public DatosPbipMongo? DatosPbip { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class AgenciaMongo
    {
        [BsonElement("rol")]
        public string Rol { get; set; } = string.Empty;

        [BsonElement("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [BsonElement("contacto")]
        public string Contacto { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements]
    public class DatosPbipMongo
    {
        [BsonElement("contactoOcpm")]
        public string ContactoOcpm { get; set; } = string.Empty;

        [BsonElement("nroInmarsat")]
        public string NroInmarsat { get; set; } = string.Empty;

        [BsonElement("arqueoBruto")]
        public double ArqueoBruto { get; set; }

        [BsonElement("nivelProteccion")]
        public int NivelProteccion { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class NotaBitacoraMongo
    {
        [BsonElement("id")]
        public string Id { get; set; } = string.Empty;

        [BsonElement("texto")]
        public string Texto { get; set; } = string.Empty;

        [BsonElement("usuario")]
        public string Usuario { get; set; } = string.Empty;

        [BsonElement("fechaHora")]
        public DateTime FechaHora { get; set; }

        [BsonElement("categoria")]
        public string Categoria { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────────

    [BsonIgnoreExtraElements]
    public class PbipMongo
    {
        [BsonElement("nroInmarsat")]
        [BsonIgnoreIfNull]
        public string NroInmarsat { get; set; } = string.Empty;

        [BsonElement("arqueoBruto")]
        public double ArqueoBruto { get; set; }

        [BsonElement("nivelProteccion")]
        public int NivelProteccion { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class InspectorMongo
    {
        [BsonElement("documento")]
        [BsonIgnoreIfNull]
        public string Documento { get; set; } = string.Empty;

        [BsonElement("nombreApellido")]
        [BsonIgnoreIfNull]
        public string NombreApellido { get; set; } = string.Empty;

        [BsonElement("fechaEmbarque")]
        public DateTime FechaEmbarque { get; set; }

        [BsonElement("fechaDesembarque")]
        [BsonIgnoreIfNull]
        public DateTime? FechaDesembarque { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class PracticoMongo
    {
        [BsonElement("documento")]
        [BsonIgnoreIfNull]
        public string Documento { get; set; } = string.Empty;

        [BsonElement("nombreApellido")]
        [BsonIgnoreIfNull]
        public string NombreApellido { get; set; } = string.Empty;

        [BsonElement("fechaEmbarque")]
        public DateTime FechaEmbarque { get; set; }

        [BsonElement("fechaDesembarque")]
        [BsonIgnoreIfNull]
        public DateTime? FechaDesembarque { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class EtapaMongo
    {
        [BsonElement("ETAPA_ID")]
        [BsonIgnoreIfNull]
        public long? EtapaId { get; set; }

        [BsonElement("FECHA_INICIO")]
        [BsonIgnoreIfNull]
        public DateTime? FechaInicio { get; set; }

        [BsonElement("FECHA_FIN")]
        [BsonIgnoreIfNull]
        public DateTime? FechaFin { get; set; }

        [BsonElement("REMOLCADOR")]
        [BsonIgnoreIfNull]
        public RemolcadorMongo? Remolcador { get; set; }

        // Se mantiene tal cual: es el formato canónico nuevo ("BARCAZAS" en mayúscula
        // dentro de cada etapa). No se toca para no romper documentos modernos.
        [BsonElement("BARCAZAS")]
        [BsonIgnoreIfNull]
        public List<BarcazaMongo>? Barcazas { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    [BsonIgnoreExtraElements]
    public class RemolcadorMongo
    {
        [BsonElement("NOMBRE")]
        [BsonIgnoreIfNull]
        public string? Nombre { get; set; }

        [BsonElement("MATRICULA")]
        [BsonIgnoreIfNull]
        public string? Matricula { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    [BsonIgnoreExtraElements]
    public class BarcazaMongo
    {
        // FIX: documentos legacy pueden traer idViaje como BsonNull → long? evita FormatException
        [BsonElement("ID_VIAJE")]
        [BsonIgnoreIfNull]
        public long? IdViaje { get; set; }

        [BsonElement("BARCAZA")]
        [BsonIgnoreIfNull]
        public string? Nombre { get; set; }

        [BsonElement("BANDERA")]
        [BsonIgnoreIfNull]
        public string? Bandera { get; set; }

        [BsonElement("MATRICULA")]
        [BsonIgnoreIfNull]
        public string? Matricula { get; set; }

        [BsonElement("CARGA")]
        [BsonIgnoreIfNull]
        public string? Carga { get; set; }

        [BsonElement("CANTIDAD")]
        [BsonIgnoreIfNull]
        public double? Cantidad { get; set; }

        [BsonElement("UNIDAD")]
        [BsonIgnoreIfNull]
        public string? Unidad { get; set; }

        [BsonElement("MUELLE_ACTUAL")]
        [BsonIgnoreIfNull]
        public string? MuelleActual { get; set; }

        [BsonElement("MERCADERIA_ID")]
        [BsonIgnoreIfNull]
        public int? MercaderiaId { get; set; }

        /// <summary>
        /// Indica descarga completa registrada (dual-path con validación de finalización de viaje).
        /// </summary>
        [BsonElement("DESCARGADA")]
        [BsonIgnoreIfNull]
        public bool? Descargada { get; set; }

        // ── PROPIEDADES DE NEGOCIO (nunca se serializan) ─────────────────────────

        /// <summary>
        /// Descripción amigable de la unidad de medida para presentación en UI.
        /// </summary>
        [BsonIgnore]
        public string UnidadDescripcion => Unidad?.ToUpperInvariant() switch
        {
            "TN"  => "Toneladas",
            "M3"  => "Metros Cúbicos",
            "BBL" => "Barriles",
            "KG"  => "Kilogramos",
            _     => Unidad ?? string.Empty
        };

        /// <summary>
        /// Indica si la barcaza lleva carga líquida según el tipo de mercadería.
        /// </summary>
        [BsonIgnore]
        public bool EsCargaLiquida =>
            Carga != null &&
            (Carga.Contains("petroleo", StringComparison.OrdinalIgnoreCase) ||
             Carga.Contains("combustible", StringComparison.OrdinalIgnoreCase) ||
             Carga.Contains("gas", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Resumen compacto para logs y trazabilidad.
        /// </summary>
        [BsonIgnore]
        public string Resumen =>
            $"[{Matricula ?? "SIN MATRÍCULA"}] {Nombre ?? "SIN NOMBRE"} — {Cantidad?.ToString("N2") ?? "0"} {Unidad}";
    }
}