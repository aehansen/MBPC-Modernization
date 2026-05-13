// Mbpc.Api/DTOs/PersonalViajeDto.cs
// Hito 9.0 — DTO de lectura para personal externo embarcado en un viaje.

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// Proyección de lectura del personal externo embarcado en un viaje.
    /// Construido desde InspectorMongo y PracticoMongo del documento ViajeDetalleMongo.
    /// </summary>
    public class PersonalViajeDto
    {
        public List<PersonalItemDto> Inspectores { get; set; } = new();
        public List<PersonalItemDto> Practicos   { get; set; } = new();
    }

    /// <summary>
    /// Ítem individual de personal embarcado (Inspector o Práctico).
    /// FechaDesembarque == null indica que el personal sigue a bordo.
    /// </summary>
    public class PersonalItemDto
    {
        public string   Documento        { get; set; } = string.Empty;
        public string   NombreApellido   { get; set; } = string.Empty;
        public DateTime FechaEmbarque    { get; set; }
        public DateTime? FechaDesembarque { get; set; }

        /// <summary>
        /// true si FechaDesembarque es null → persona aún activa a bordo.
        /// Calculado en la capa de proyección; nunca persiste.
        /// </summary>
        public bool EstaABordo => FechaDesembarque is null;
    }
}
