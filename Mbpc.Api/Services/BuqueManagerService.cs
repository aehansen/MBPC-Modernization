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
        /// En producción, propaga la excepción para que el middleware de errores
        /// la capture y devuelva un 502 al cliente.
        /// En desarrollo, loguea una advertencia y retorna lista vacía para no
        /// bloquear el ciclo de desarrollo cuando Oracle no está disponible.
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

            _logger.LogWarning("Oracle Offline. Devolviendo Buque de Mock para pruebas.");

            // Retornamos un buque ficticio para que Andy pueda seleccionarlo en el frontend
            return new List<BuqueAutocompleteDto>
            {
                new BuqueAutocompleteDto { 
                    IdBuque = 1936127, 
                    Nombre = "BUQUE DE PRUEBA MBPC", 
                    Matricula = "MOCK-001", 
                    Omi = "9999999", 
                    Tipo = "Remolcador" 
                },
                new BuqueAutocompleteDto { 
                    IdBuque = 2000001, 
                    Nombre = "PUMA (MOCK)", 
                    Matricula = "MOCK-002", 
                    Omi = "8888888", 
                    Tipo = "Barcaza"
                }        
            }; 
        } 
    } 
} 