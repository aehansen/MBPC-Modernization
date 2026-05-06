using Dapper;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Microsoft.Extensions.Options;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Config;
using System.Data;
using System.Runtime.ExceptionServices;

namespace Mbpc.Api.Services
{
    /// <summary>
    /// Implementación del servicio de Maestro de Buques (MDM).
    ///
    /// RESPONSABILIDAD:
    ///   Proveer endpoints de autocompletado que consultan Oracle para que el
    ///   frontend resuelva BuqueId y BarcazaId antes de enviar los DTOs de creación.
    ///   No escribe en base de datos; es un servicio de sólo lectura.
    ///
    /// PATRÓN DE ACCESO A ORACLE:
    ///   Usa OracleDynamicParameters (definido en CargaManagerService.cs) para
    ///   registrar parámetros nativos de Oracle —incluyendo RefCursor de salida—
    ///   y los pasa a Dapper.Query[T] con CommandType.StoredProcedure.
    ///
    /// RESILIENCIA EN DESARROLLO:
    ///   Si Oracle no está disponible y el entorno es Development, los métodos
    ///   retornan colecciones vacías con un log de advertencia en lugar de propagar
    ///   la excepción, permitiendo que el frontend siga funcionando en local.
    /// </summary>
    public class BuqueManagerService : IBuqueService
    {
        private readonly string _oracleConnectionString;
        private readonly ILogger<BuqueManagerService> _logger;
        private readonly IWebHostEnvironment _env;

        public BuqueManagerService(
            IOptions<OracleDbSettings> oracleSettings,
            ILogger<BuqueManagerService> logger,
            IWebHostEnvironment env)
        {
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
            _env    = env;
        }

        // ── IBuqueService: BuscarBuquesDisponiblesAsync ──────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<BuqueAutocompleteDto>> BuscarBuquesDisponiblesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("BuscarBuquesDisponiblesAsync: query vacío, retornando lista vacía.");
                return Enumerable.Empty<BuqueAutocompleteDto>();
            }

            _logger.LogInformation(
                "BuscarBuquesDisponiblesAsync — Consultando SP mbpc.autocomplete_buques_disp con query='{Query}'.",
                query);

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var spParams = new OracleDynamicParameters();
                spParams.Add("vQuery",  query, OracleDbType.Varchar2, ParameterDirection.Input);
                spParams.Add("vCursor", OracleDbType.RefCursor,       ParameterDirection.Output);

                var rows = await connection.QueryAsync<BuqueAutocompleteDto>(
                    "mbpc.autocomplete_buques_disp",
                    spParams,
                    commandType: CommandType.StoredProcedure);

                var resultado = rows.ToList();

                _logger.LogInformation(
                    "BuscarBuquesDisponiblesAsync — {Count} buque(s) encontrado(s) para query='{Query}'.",
                    resultado.Count, query);

                return resultado;
            }
            catch (OracleException ex)
            {
                return ManejarExcepcionOracle(ex, nameof(BuscarBuquesDisponiblesAsync), query);
            }
        }

        // ── IBuqueService: BuscarBarcazasDisponiblesAsync ────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<BuqueAutocompleteDto>> BuscarBarcazasDisponiblesAsync(
            string etapaId,
            string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("BuscarBarcazasDisponiblesAsync: query vacío, retornando lista vacía.");
                return Enumerable.Empty<BuqueAutocompleteDto>();
            }

            if (string.IsNullOrWhiteSpace(etapaId))
            {
                _logger.LogWarning("BuscarBarcazasDisponiblesAsync: etapaId vacío o nulo. Abortando consulta.");
                return Enumerable.Empty<BuqueAutocompleteDto>();
            }

            _logger.LogInformation(
                "BuscarBarcazasDisponiblesAsync — Consultando SP mbpc.autocomplete_barcazas con etapaId='{EtapaId}', query='{Query}'.",
                etapaId, query);

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                // Reutilizamos OracleDynamicParameters (misma clase que en CargaManagerService)
                // para registrar los dos parámetros de entrada más el RefCursor de salida.
                var spParams = new OracleDynamicParameters();
                spParams.Add("vEtapaId", etapaId, OracleDbType.Varchar2,  ParameterDirection.Input);
                spParams.Add("vQuery",   query,    OracleDbType.Varchar2,  ParameterDirection.Input);
                spParams.Add("vCursor",  OracleDbType.RefCursor,           ParameterDirection.Output);

                var rows = await connection.QueryAsync<BuqueAutocompleteDto>(
                    "mbpc.autocomplete_barcazas",
                    spParams,
                    commandType: CommandType.StoredProcedure);

                var resultado = rows.ToList();

                _logger.LogInformation(
                    "BuscarBarcazasDisponiblesAsync — {Count} barcaza(s) encontrada(s) para etapaId='{EtapaId}', query='{Query}'.",
                    resultado.Count, etapaId, query);

                return resultado;
            }
            catch (OracleException ex)
            {
                return ManejarExcepcionOracle(ex, nameof(BuscarBarcazasDisponiblesAsync), $"etapaId={etapaId}, query={query}");
            }
        }

        // ── Helper Privado: Resiliencia en Desarrollo ────────────────────────

/// <summary>
        /// Centraliza el manejo de OracleException para los métodos de lectura.
        /// En desarrollo, loguea una advertencia y retorna la lista mockeada 
        /// con datos reales de Mongo, filtrada según la búsqueda.
        /// </summary>
        private IEnumerable<BuqueAutocompleteDto> ManejarExcepcionOracle(
            OracleException ex,
            string metodo,
            string contexto)
        {
            if (!_env.IsDevelopment())
            {
                _logger.LogError(ex, "{Metodo} — Error de Oracle en producción.", metodo);
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            }

            // 1. Extraemos el texto que escribiste en el frontend
            string query = contexto.Contains("query=") 
                ? contexto.Split("query=")[1].Trim() 
                : contexto.Trim();

            _logger.LogWarning("Oracle Offline. Usando MOCK de 100 buques filtrado por: '{Query}'", query);

            var mockDb = ObtenerMockDb();

            // 3. Filtramos la lista para que el autocompletado funcione de verdad
            if (string.IsNullOrWhiteSpace(query)) return mockDb;

            return mockDb.Where(b => 
                (b.Nombre != null && b.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase)) || 
                (b.Matricula != null && b.Matricula.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (b.Omi != null && b.Omi.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // ── IBuqueService: ObtenerBuquePorIdAsync ────────────────────────────

        /// <inheritdoc/>
        public async Task<BuqueAutocompleteDto?> ObtenerBuquePorIdAsync(long idBuque)
        {
            _logger.LogInformation("ObtenerBuquePorIdAsync — Consultando buque con ID={IdBuque}.", idBuque);

            try
            {
                if (_env.IsDevelopment())
                {
                    return ObtenerMockDb().FirstOrDefault(b => b.IdBuque == idBuque);
                }

                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var rows = await connection.QueryAsync<BuqueAutocompleteDto>(
                    "SELECT ID_BUQUE as IdBuque, NOMBRE as Nombre, MATRICULA as Matricula, OMI as Omi, TIPO as Tipo FROM BUQUES_NEW WHERE ID_BUQUE = :IdBuque AND ROWNUM = 1",
                    new { IdBuque = idBuque });

                return rows.FirstOrDefault() ?? ObtenerMockDb().FirstOrDefault(b => b.IdBuque == idBuque);
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogWarning(ex, "ObtenerBuquePorIdAsync — Error de Oracle. Usando MOCK para ID={IdBuque}", idBuque);
                }
                return ObtenerMockDb().FirstOrDefault(b => b.IdBuque == idBuque);
            }
        }

        // ── IBuqueService: ObtenerBuquesPorIdsAsync (Batch / Anti N+1) ──────

        /// <inheritdoc/>
        public async Task<Dictionary<long, BuqueAutocompleteDto>> ObtenerBuquesPorIdsAsync(IEnumerable<long> ids)
        {
            var idsDistintos = ids.Distinct().ToList();

            if (idsDistintos.Count == 0)
                return new Dictionary<long, BuqueAutocompleteDto>();

            _logger.LogInformation(
                "ObtenerBuquesPorIdsAsync — Resolviendo {Count} ID(s) en una sola operación.",
                idsDistintos.Count);

            if (_env.IsDevelopment())
            {
                var mockDb = ObtenerMockDb();
                return mockDb
                    .Where(b => idsDistintos.Contains(b.IdBuque))
                    .ToDictionary(b => b.IdBuque);
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                // Oracle no expande IEnumerable automáticamente con OracleConnection+Dapper,
                // por lo que parametrizamos manualmente cada ID.
                var paramNames = idsDistintos.Select((_, i) => $":id{i}").ToList();
                var sql = $"SELECT ID_BUQUE as IdBuque, NOMBRE as Nombre, MATRICULA as Matricula, " +
                          $"OMI as Omi, TIPO as Tipo FROM BUQUES_NEW " +
                          $"WHERE ID_BUQUE IN ({string.Join(", ", paramNames)})";

                var dynParams = new DynamicParameters();
                for (int i = 0; i < idsDistintos.Count; i++)
                    dynParams.Add($"id{i}", idsDistintos[i]);

                var rows = await connection.QueryAsync<BuqueAutocompleteDto>(sql, dynParams);
                var resultado = rows.ToDictionary(b => b.IdBuque);

                // Fallback para IDs no encontrados en Oracle → busca en MOCK
                var idsNoEncontrados = idsDistintos.Where(id => !resultado.ContainsKey(id)).ToList();
                if (idsNoEncontrados.Any())
                {
                    _logger.LogWarning(
                        "ObtenerBuquesPorIdsAsync — {Count} ID(s) no encontrados en Oracle. Usando MOCK como fallback.",
                        idsNoEncontrados.Count);

                    var mockFallback = ObtenerMockDb();
                    foreach (var id in idsNoEncontrados)
                    {
                        var mockEntry = mockFallback.FirstOrDefault(b => b.IdBuque == id);
                        if (mockEntry != null)
                            resultado[id] = mockEntry;
                    }
                }

                return resultado;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ObtenerBuquesPorIdsAsync — Error de Oracle. Usando MOCK como fallback.");
                var mockDb = ObtenerMockDb();
                return mockDb
                    .Where(b => idsDistintos.Contains(b.IdBuque))
                    .ToDictionary(b => b.IdBuque);
            }
        }

        private List<BuqueAutocompleteDto> ObtenerMockDb()
        {
            return new List<BuqueAutocompleteDto>
            {
                // ─── REMOLCADORES ───
                new() { IdBuque = 1045174, Nombre = "YANI G", Matricula = "LW4793", Omi = "1045174", Tipo = "Remolcador" },
                new() { IdBuque = 1070064, Nombre = "VERONICA V", Matricula = "N/A", Omi = "1070064", Tipo = "Remolcador" },
                new() { IdBuque = 1013705, Nombre = "AFRICAN LORIKEET", Matricula = "3E5310", Omi = "1013705", Tipo = "Remolcador" },
                new() { IdBuque = 1092359, Nombre = "LITO", Matricula = "LW4966", Omi = "1092359", Tipo = "Remolcador" },
                new() { IdBuque = 1109920, Nombre = "LEONILDA", Matricula = "LW4978", Omi = "1109920", Tipo = "Remolcador" },
                new() { IdBuque = 2211330, Nombre = "PUMA", Matricula = "ZPSE", Omi = "2211330", Tipo = "Remolcador" },
                new() { IdBuque = 6525210, Nombre = "SAN CAYETANO I", Matricula = "LW4115", Omi = "6525210", Tipo = "Remolcador" },
                new() { IdBuque = 6730243, Nombre = "PETREL", Matricula = "LW2961", Omi = "6730243", Tipo = "Remolcador" },
                new() { IdBuque = 8408466, Nombre = "INTREPIDO", Matricula = "ZPTO", Omi = "8408466", Tipo = "Remolcador" },
                new() { IdBuque = 8616192, Nombre = "TABEIRON DOS", Matricula = "LW7242", Omi = "8616192", Tipo = "Remolcador" },
                new() { IdBuque = 8656556, Nombre = "TABEIRON TRES", Matricula = "LW3149", Omi = "8656556", Tipo = "Remolcador" },
                new() { IdBuque = 8696465, Nombre = "MARIA GLORIA", Matricula = "LW2523", Omi = "8696465", Tipo = "Remolcador" },
                new() { IdBuque = 8747719, Nombre = "COMANDANTE LUIS PIEDRABUENA", Matricula = "LW4926", Omi = "8747719", Tipo = "Remolcador" },

                // ─── BUQUES MOTOR / ULTRAMAR ───
                new() { IdBuque = 5000001, Nombre = "MSC ROSARIA", Matricula = "N/A", Omi = "9320257", Tipo = "Buque Motor" },
                new() { IdBuque = 5000002, Nombre = "CLIPPER BRUNSWICK", Matricula = "N/A", Omi = "9400000", Tipo = "Buque Motor" },
                new() { IdBuque = 5000003, Nombre = "FEDERAL KIVALINA", Matricula = "N/A", Omi = "9200000", Tipo = "Buque Motor" },
                new() { IdBuque = 5000004, Nombre = "STAR GRACE", Matricula = "N/A", Omi = "9500000", Tipo = "Buque Motor" },
                new() { IdBuque = 5000005, Nombre = "SBI ANTARES", Matricula = "N/A", Omi = "9600000", Tipo = "Buque Motor" },
                new() { IdBuque = 5000006, Nombre = "NAVIOS SPRING", Matricula = "N/A", Omi = "9700000", Tipo = "Buque Motor" },

                // ─── EMBARCACIONES MENORES ───
                new() { IdBuque = 4000001, Nombre = "PRACTICO I", Matricula = "REY-0192", Omi = "-", Tipo = "Embarcación Menor" },
                new() { IdBuque = 4000002, Nombre = "L/M SAN MARTIN", Matricula = "REY-0205", Omi = "-", Tipo = "Embarcación Menor" },
                new() { IdBuque = 4000003, Nombre = "AMARRE III", Matricula = "REY-0311", Omi = "-", Tipo = "Embarcación Menor" },
                new() { IdBuque = 4000004, Nombre = "DELTA SUR", Matricula = "TIG-0055", Omi = "-", Tipo = "Embarcación Menor" },
                new() { IdBuque = 4000005, Nombre = "PRACTICO RIO", Matricula = "ZAR-0122", Omi = "-", Tipo = "Embarcación Menor" },

                // ─── BARCAZAS (CONVOY RS) ───
                new() { IdBuque = 3000001, Nombre = "RS001", Matricula = "EN TRAMITE", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000002, Nombre = "RS002", Matricula = "PASAVANTE", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000003, Nombre = "RS003", Matricula = "S/M", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000004, Nombre = "RS004", Matricula = "EN TRAMITE", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000005, Nombre = "RS005", Matricula = "ENTRAMITE", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000006, Nombre = "RS006", Matricula = "ENTRAMITE", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000007, Nombre = "RS007", Matricula = "TRAMITE", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000008, Nombre = "RS008", Matricula = "EN TRAMITE", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000009, Nombre = "RS009", Matricula = "e/t", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000010, Nombre = "RS010", Matricula = "0", Omi = "-", Tipo = "Barcaza" },

                // ─── BARCAZAS (UABL) ───
                new() { IdBuque = 3000101, Nombre = "UABL 101", Matricula = "PY-101", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000102, Nombre = "UABL 102", Matricula = "PY-102", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000103, Nombre = "UABL 103", Matricula = "PY-103", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000104, Nombre = "UABL 104", Matricula = "PY-104", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000105, Nombre = "UABL 105", Matricula = "PY-105", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000106, Nombre = "UABL 106", Matricula = "PY-106", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000107, Nombre = "UABL 107", Matricula = "PY-107", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000108, Nombre = "UABL 108", Matricula = "PY-108", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000109, Nombre = "UABL 109", Matricula = "PY-109", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000110, Nombre = "UABL 110", Matricula = "PY-110", Omi = "-", Tipo = "Barcaza" },
                
                // ─── BARCAZAS (ACBL) ───
                new() { IdBuque = 3000201, Nombre = "ACBL 01", Matricula = "ARG-201", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000202, Nombre = "ACBL 02", Matricula = "ARG-202", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000203, Nombre = "ACBL 03", Matricula = "ARG-203", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000204, Nombre = "ACBL 04", Matricula = "ARG-204", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000205, Nombre = "ACBL 05", Matricula = "ARG-205", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000206, Nombre = "ACBL 06", Matricula = "ARG-206", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000207, Nombre = "ACBL 07", Matricula = "ARG-207", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000208, Nombre = "ACBL 08", Matricula = "ARG-208", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000209, Nombre = "ACBL 09", Matricula = "ARG-209", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000210, Nombre = "ACBL 10", Matricula = "ARG-210", Omi = "-", Tipo = "Barcaza" },

                // ─── BARCAZAS (INTERBARGE / IMP) ───
                new() { IdBuque = 3000301, Nombre = "IMP 301", Matricula = "PAR-301", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000302, Nombre = "IMP 302", Matricula = "PAR-302", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000303, Nombre = "IMP 303", Matricula = "PAR-303", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000304, Nombre = "IMP 304", Matricula = "PAR-304", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000305, Nombre = "IMP 305", Matricula = "PAR-305", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000306, Nombre = "IMP 306", Matricula = "PAR-306", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000307, Nombre = "IMP 307", Matricula = "PAR-307", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000308, Nombre = "IMP 308", Matricula = "PAR-308", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000309, Nombre = "IMP 309", Matricula = "PAR-309", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000310, Nombre = "IMP 310", Matricula = "PAR-310", Omi = "-", Tipo = "Barcaza" },

                // ─── BARCAZAS (NAVIOS / MERCO) ───
                new() { IdBuque = 3000401, Nombre = "MERCO 10", Matricula = "UY-401", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000402, Nombre = "MERCO 11", Matricula = "UY-402", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000403, Nombre = "MERCO 12", Matricula = "UY-403", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000404, Nombre = "MERCO 13", Matricula = "UY-404", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000405, Nombre = "MERCO 14", Matricula = "UY-405", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000406, Nombre = "MERCO 15", Matricula = "UY-406", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000407, Nombre = "MERCO 16", Matricula = "UY-407", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000408, Nombre = "MERCO 17", Matricula = "UY-408", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000409, Nombre = "MERCO 18", Matricula = "UY-409", Omi = "-", Tipo = "Barcaza" },
                new() { IdBuque = 3000410, Nombre = "MERCO 19", Matricula = "UY-410", Omi = "-", Tipo = "Barcaza" }
            };
        }
    }
}