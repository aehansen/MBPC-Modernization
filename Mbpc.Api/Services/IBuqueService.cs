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
    }
}
