using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Mbpc.Api.Models.Mongo
{
    /// <summary>
    /// Documento de detalle operativo de un viaje almacenado en MongoDB.
    /// Complementa a ViajePosicionMongo con datos de negocio provenientes de Oracle
    /// (remolcador, barcazas, etc.) que se sincronizan vía CQRS.
    ///
    /// MIGRACIÓN ESTRUCTURAL (Cimientos NoSQL):
    ///   Los campos <c>Remolcador</c> y <c>Barcazas</c> que antes vivían en la raíz
    ///   del documento han sido movidos al interior de cada <see cref="EtapaMongo"/>.
    ///   Esto refleja la estructura real del sistema legacy donde cada viaje tiene
    ///   una o más etapas, y cada etapa posee su propio remolcador y conjunto de barcazas.
    ///
    ///   COMPATIBILIDAD DE LECTURA:
    ///   El atributo [BsonIgnoreExtraElements] garantiza que documentos legacy que aún
    ///   tengan <c>remolcador</c> y <c>barcazas</c> en la raíz sean deserializados
    ///   sin error. La migración de datos en MongoDB (actualización de documentos viejos)
    ///   debe ejecutarse como script separado fuera de este servicio.
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

        /// <summary>
        /// Array de etapas del viaje. Cada etapa contiene su propio remolcador
        /// y lista de barcazas, replicando la estructura del sistema legacy Oracle.
        /// Se inicializa como lista vacía para evitar nulos en documentos nuevos.
        /// </summary>
        [BsonElement("etapas")]
        public List<EtapaMongo> Etapas { get; set; } = new();

        // ── MULTITENANT GEOGRÁFICO ──
        // El campo CosteraId en BSON es numérico (Int32).
        // CosteraId == 0  →  registro global / Super Admin.
        // CosteraId  > 0  →  registro restringido a esa jurisdicción costera.
        [BsonElement("CosteraId")]
        public int? CosteraId { get; set; }
    }

    /// <summary>
    /// Representa una etapa dentro de un viaje.
    /// Una etapa es la unidad operativa del sistema legacy (TBL_ETAPA) que agrupa
    /// el remolcador y las barcazas asignadas para un tramo específico del viaje.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class EtapaMongo
    {
        /// <summary>
        /// ID de la etapa en el sistema legacy Oracle (TBL_ETAPA.ETAPA_ID).
        /// Es el pivot que vincula este documento Mongo con el registro relacional.
        /// </summary>
        [BsonElement("EtapaId")]
        public long EtapaId { get; set; }

        /// <summary>
        /// Fecha y hora de inicio de la etapa.
        /// Opcional: puede ser nula si la etapa fue creada pero aún no iniciada.
        /// </summary>
        [BsonElement("FechaInicio")]
        public DateTime? FechaInicio { get; set; }

        /// <summary>
        /// Remolcador asignado a esta etapa del viaje.
        /// Opcional: puede ser nulo si la etapa no requiere remolque.
        /// </summary>
        [BsonElement("remolcador")]
        public RemolcadorMongo? Remolcador { get; set; }

        /// <summary>
        /// Lista de barcazas (con su carga) asignadas a esta etapa.
        /// Puede ser nula o vacía si no hay barcazas en la etapa.
        /// </summary>
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
