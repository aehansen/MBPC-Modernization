namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// Proyección de un buque del padrón BUQUES_NEW devuelta por el endpoint
    /// de autocompletado. Contiene únicamente los campos necesarios para que el
    /// frontend muestre la lista de sugerencias y resuelva el BuqueId a enviar
    /// en NuevoViajeDto.
    ///
    /// Esta clase es de sólo lectura (sin setters públicos en producción futura);
    /// por ahora se mantienen las propiedades auto para compatibilidad con Dapper.
    /// </summary>
    public class BuqueAutocompleteDto
    {
        /// <summary>Identificador numérico del buque en el padrón BUQUES_NEW.</summary>
        public long IdBuque { get; set; }

        /// <summary>Nombre comercial o de matrícula del buque.</summary>
        public string? Nombre { get; set; }

        /// <summary>Número IMO (International Maritime Organization) del buque.</summary>
        public string? Omi { get; set; }

        /// <summary>Código de distrito (Sdist) del buque en el sistema legacy.</summary>
        public string? Sdist { get; set; }

        /// <summary>Matrícula oficial del buque.</summary>
        public string? Matricula { get; set; }

        /// <summary>Bandera (país de registro) del buque.</summary>
        public string? Bandera { get; set; }

        /// <summary>Tipo de embarcación (ej: Remolcador, Barcaza, Buque de Carga, etc.).</summary>
        public string? Tipo { get; set; }

        /// <summary>Estado operativo del buque según el padrón (ej: Activo, Dado de baja).</summary>
        public string? Estado { get; set; }

        /// <summary>
        /// Costera a la que pertenece el buque, en caso de restricción jurisdiccional.
        /// Null indica que el buque es visible para todas las costeras.
        /// </summary>
        public string? Costera { get; set; }
    }
}
