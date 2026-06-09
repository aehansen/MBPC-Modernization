using Mbpc.Api.DTOs;
using Mbpc.Api.DTOs.Catalogos;

namespace Mbpc.Api.Services
{
    /// <summary>
    /// Contrato del servicio de catálogos del sistema MBPC.
    ///
    /// RESPONSABILIDADES:
    ///   - Proveer lectura del catálogo maestro de Puntos de Control, Muelles, Buques y Barcazas.
    ///   - Encapsular la lógica de validación de integridad antes de cada alta/edición
    ///     (unicidad de OMI, MMSI, matrícula, código de muelle).
    ///   - Ejecutar los Stored Procedures Oracle de ABM vía Dapper.
    ///   - Las validaciones de negocio fallidas se lanzan como InvalidOperationException,
    ///     que el Middleware global de excepciones convierte en HTTP 422.
    /// </summary>
    public interface ICatalogoService
    {
        // ── Puntos de Control ─────────────────────────────────────────────────

        /// <summary>Lista todos los puntos de control del sistema.</summary>
        Task<IEnumerable<PuntoControlDto>> ObtenerPuntosControlAsync();

        // ── Muelles ───────────────────────────────────────────────────────────

        /// <summary>Lista todos los muelles activos. DEV-BYPASS incluido.</summary>
        Task<IEnumerable<MuelleDto>> ObtenerMuellesAsync();

        /// <summary>
        /// Retorna el detalle completo de un muelle por ID.
        /// Retorna null si no existe.
        /// </summary>
        Task<MuelleDetalleDto?> ObtenerMuellePorIdAsync(int id);

        /// <summary>
        /// Crea un nuevo muelle en Oracle mediante SP legacy.
        /// Valida unicidad del Código si se informa.
        /// Lanza InvalidOperationException si el código ya existe.
        /// </summary>
        /// <param name="dto">Datos del nuevo muelle.</param>
        /// <param name="costeraId">CosteraId extraído del JWT por el controller.</param>
        Task<MuelleDetalleDto> CrearMuelleAsync(MuelleAltaDto dto, int costeraId);

        /// <summary>
        /// Edita un muelle existente. Valida unicidad del Código excluyendo el propio registro.
        /// Lanza InvalidOperationException si el Id no existe o si el Código ya pertenece a otro muelle.
        /// </summary>
        Task<MuelleDetalleDto> EditarMuelleAsync(MuelleEditDto dto);

        // ── Buques ────────────────────────────────────────────────────────────

        /// <summary>
        /// Lista paginada de buques del padrón. Permite filtrar por nombre, matrícula o OMI.
        /// </summary>
        /// <param name="query">Texto libre de búsqueda (null → sin filtro).</param>
        /// <param name="pagina">Número de página (1-indexed).</param>
        /// <param name="tamanio">Registros por página (máx. 100).</param>
        Task<IEnumerable<BuqueDetalleDto>> ObtenerBuquesAsync(string? query, int pagina, int tamanio);

        /// <summary>
        /// Retorna el detalle completo de un buque por IdBuque.
        /// Retorna null si no existe en el padrón.
        /// </summary>
        Task<BuqueDetalleDto?> ObtenerBuquePorIdAsync(long idBuque);

        /// <summary>
        /// Crea un nuevo buque en Oracle.
        /// - Si NroOmi informado → SP <c>mbpc.crear_buque</c> (bandera extranjera/ultramar).
        /// - Si NroOmi nulo     → SP <c>mbpc.crear_buque_int</c> (bandera interior).
        /// Valida unicidad de OMI, MMSI y Matrícula antes de invocar el SP.
        /// Lanza InvalidOperationException ante colisión de OMI, MMSI o Matrícula.
        /// </summary>
        /// <param name="dto">Datos del nuevo buque.</param>
        /// <param name="costeraId">CosteraId extraído del JWT por el controller.</param>
        Task<BuqueDetalleDto> CrearBuqueAsync(BuqueAltaDto dto, int costeraId);

        /// <summary>
        /// Edita los datos de un buque existente.
        /// Valida unicidad de OMI/MMSI/Matrícula excluyendo el propio registro.
        /// Lanza InvalidOperationException si la validación falla.
        /// </summary>
        Task<BuqueDetalleDto> EditarBuqueAsync(BuqueEditDto dto);

        // ── Barcazas ──────────────────────────────────────────────────────────

        /// <summary>
        /// Lista paginada de barcazas del padrón. Permite filtrar por nombre o matrícula.
        /// </summary>
        Task<IEnumerable<BarcazaDetalleDto>> ObtenerBarcazasAsync(string? query, int pagina, int tamanio);

        /// <summary>
        /// Retorna el detalle completo de una barcaza por IdBarcaza.
        /// Retorna null si no existe.
        /// </summary>
        Task<BarcazaDetalleDto?> ObtenerBarcazaPorIdAsync(long idBarcaza);

        /// <summary>
        /// Crea una nueva barcaza en Oracle vía SP <c>mbpc.crear_barcaza</c>.
        /// Valida unicidad de matrícula si se informa (excepto valores especiales como 'EN TRAMITE').
        /// Lanza InvalidOperationException si la matrícula ya existe en el padrón.
        /// </summary>
        /// <param name="dto">Datos de la nueva barcaza.</param>
        /// <param name="costeraId">CosteraId extraído del JWT por el controller.</param>
        Task<BarcazaDetalleDto> CrearBarcazaAsync(BarcazaAltaDto dto, int costeraId);

        /// <summary>
        /// Edita los datos de una barcaza existente.
        /// Valida unicidad de matrícula excluyendo el propio registro.
        /// Lanza InvalidOperationException si la validación falla.
        /// </summary>
        Task<BarcazaDetalleDto> EditarBarcazaAsync(BarcazaEditDto dto);
    }
}
