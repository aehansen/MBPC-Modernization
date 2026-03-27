// ViajeDetalleMongo.cs
// Dominio Mongo — Eje 1 + Eje 2 (EstadoEtapa como Enum con serialización BSON string).
// Namespace: Mbpc.Api.Models.Mongo

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mbpc.Api.Models.Mongo
{
    // ══════════════════════════════════════════════════════════════════════════
    // EJE 2 — Enum de estados fuertemente tipado
    // Se serializa como String en MongoDB para legibilidad y compatibilidad
    // con el legado que guardaba strings sueltos ("Navegando", "Amarrado", etc.)
    // ══════════════════════════════════════════════════════════════════════════
    public enum EstadoEtapa
    {
        Navegando,
        Fondeado,
        Amarrado,
        Reanudado
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Raíz del agregado de detalle de viaje
    // ══════════════════════════════════════════════════════════════════════════
    [BsonIgnoreExtraElements]
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

        /// <summary>
        /// Vínculo con la costera operativa. Nullable: no todos los viajes tienen costera asignada.
        /// </summary>
        [BsonElement("costeraId")]
        [BsonIgnoreIfNull]
        public string? CosteraId { get; set; }

        [BsonElement("barcazas")]
        public List<BarcazaMongo> Barcazas { get; set; } = new List<BarcazaMongo>();

        [BsonElement("remolcador")]
        public RemolcadorMongo? Remolcador { get; set; }

        /// <summary>
        /// Secuencia ordenada de etapas del viaje (puntos de control, fondeos, etc.).
        /// Colección inicializada vacía para evitar NPE en proyecciones del servicio.
        /// </summary>
        [BsonElement("etapas")]
        public List<EtapaMongo> Etapas { get; set; } = new List<EtapaMongo>();

        /// <summary>
        /// Prácticos que intervienen en el viaje (embarque/desembarque por zona).
        /// </summary>
        [BsonElement("practicos")]
        public List<PracticoMongo> Practicos { get; set; } = new List<PracticoMongo>();

        /// <summary>
        /// Inspectores (organismos de control: Prefectura, Aduana, Senasa, etc.).
        /// </summary>
        [BsonElement("inspectores")]
        public List<InspectorMongo> Inspectores { get; set; } = new List<InspectorMongo>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EJE 2 — EtapaMongo con EstadoEtapa tipado y serializado como String
    // ══════════════════════════════════════════════════════════════════════════
    [BsonIgnoreExtraElements]
    public class EtapaMongo
    {
        /// <summary>Punto geográfico o kilométrico de control (ej: "KM 80 Paraná").</summary>
        [BsonElement("puntoControl")]
        public string? PuntoControl { get; set; }

        /// <summary>Hora real de paso (Hora Real de Paso).</summary>
        [BsonElement("hrp")]
        public string? Hrp { get; set; }

        /// <summary>Hora estimada de arribo a este punto.</summary>
        [BsonElement("eta")]
        public string? Eta { get; set; }

        /// <summary>
        /// Estado de la etapa usando el Enum fuertemente tipado.
        /// [BsonRepresentation(BsonType.String)] garantiza que MongoDB almacena
        /// "Navegando" | "Fondeado" | "Amarrado" | "Reanudado" — no un entero.
        /// Esto preserva la legibilidad del documento y la compatibilidad con el legado.
        /// </summary>
        [BsonElement("estado")]
        [BsonRepresentation(BsonType.String)]
        public EstadoEtapa Estado { get; set; }

        /// <summary>
        /// Marca si esta es la etapa vigente del viaje.
        /// Solo una etapa debe tener EsActiva = true en un momento dado.
        /// </summary>
        [BsonElement("esActiva")]
        public bool EsActiva { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Barcaza
    // ══════════════════════════════════════════════════════════════════════════
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

    // ══════════════════════════════════════════════════════════════════════════
    // Remolcador
    // ══════════════════════════════════════════════════════════════════════════
    [BsonIgnoreExtraElements]
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

    // ══════════════════════════════════════════════════════════════════════════
    // Práctico — EJE 1 nuevo
    // ══════════════════════════════════════════════════════════════════════════
    [BsonIgnoreExtraElements]
    public class PracticoMongo
    {
        [BsonElement("nombre")]
        public string Nombre { get; set; } = null!;

        /// <summary>Fecha/hora de embarque del práctico (ISO 8601 o formato operativo).</summary>
        [BsonElement("fechaEmbarque")]
        public string? FechaEmbarque { get; set; }

        /// <summary>Fecha/hora de desembarque del práctico.</summary>
        [BsonElement("fechaDesembarque")]
        public string? FechaDesembarque { get; set; }

        /// <summary>Zona de pilotaje asignada (ej: "Zona A - Río de la Plata").</summary>
        [BsonElement("zona")]
        public string? Zona { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Inspector — EJE 1 nuevo
    // ══════════════════════════════════════════════════════════════════════════
    [BsonIgnoreExtraElements]
    public class InspectorMongo
    {
        [BsonElement("nombre")]
        public string Nombre { get; set; } = null!;

        /// <summary>Organismo de control (ej: "Prefectura Naval", "Aduana", "Senasa").</summary>
        [BsonElement("organismo")]
        public string Organismo { get; set; } = null!;
    }
}