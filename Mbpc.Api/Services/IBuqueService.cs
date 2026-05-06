using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    /// <summary>
    /// Contrato del servicio de Maestro de Buques (MDM).
    /// Expone métodos de autocompletado que consultan el padrón Oracle
    /// para que el frontend pueda resolver IDs numéricos antes de enviar
    /// los DTOs de creación (NuevoViajeDto, NuevaCargaDto).
    /// </summary>
    public interface IBuqueService
    {
        /// <summary>
        /// Busca buques disponibles en el padrón BUQUES_NEW cuyo nombre,
        /// matrícula u OMI coincidan parcialmente con <paramref name="query"/>.
        /// Invoca el SP Oracle <c>mbpc.autocomplete_buques_disp</c>.
        /// </summary>
        /// <param name="query">
        /// Cadena de búsqueda ingresada por el usuario (mínimo 2 caracteres
        /// recomendado para evitar resultsets masivos).
        /// </param>
        /// <returns>
        /// Colección (posiblemente vacía) de proyecciones <see cref="BuqueAutocompleteDto"/>
        /// ordenadas por relevancia según el SP.
        /// </returns>
        Task<IEnumerable<BuqueAutocompleteDto>> BuscarBuquesDisponiblesAsync(string query);

        /// <summary>
        /// Busca barcazas disponibles para ser asignadas a la etapa indicada,
        /// filtradas por <paramref name="query"/> (nombre o matrícula parcial).
        /// Invoca el SP Oracle <c>mbpc.autocomplete_barcazas</c>.
        /// </summary>
        /// <param name="etapaId">
        /// ID de la etapa del viaje en curso. El SP usa este valor para excluir
        /// barcazas ya asignadas a la misma etapa en el sistema legacy.
        /// </param>
        /// <param name="query">
        /// Cadena de búsqueda ingresada por el usuario.
        /// </param>
        /// <returns>
        /// Colección (posiblemente vacía) de proyecciones <see cref="BuqueAutocompleteDto"/>
        /// que representan barcazas disponibles.
        /// </returns>
        Task<IEnumerable<BuqueAutocompleteDto>> BuscarBarcazasDisponiblesAsync(string etapaId, string query);

        /// <summary>
        /// Obtiene los detalles de un buque o barcaza específico por su ID.
        /// Se usa principalmente para hidratar nombres a partir de IDs guardados.
        /// </summary>
        /// <param name="idBuque">ID numérico del buque a consultar.</param>
        /// <returns>Detalle del buque, o nulo si no se encuentra.</returns>
        Task<BuqueAutocompleteDto?> ObtenerBuquePorIdAsync(long idBuque);

        /// <summary>
        /// Obtiene en una sola operación los datos de múltiples buques/barcazas
        /// por sus IDs, devolviendo un diccionario <c>IdBuque → BuqueAutocompleteDto</c>.
        /// Evita el problema N+1 al hidratar DTOs de convoy.
        /// </summary>
        /// <param name="ids">Colección de IDs numéricos a resolver. Los duplicados se ignorarán.</param>
        /// <returns>
        /// Diccionario indexado por <c>IdBuque</c>. Los IDs no encontrados
        /// simplemente no aparecerán como keys.
        /// </returns>
        Task<Dictionary<long, BuqueAutocompleteDto>> ObtenerBuquesPorIdsAsync(IEnumerable<long> ids);
    }
}
