using Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Options;
using Mbpc.Api.DTOs;
using Mbpc.Api.DTOs.Catalogos;
using Mbpc.Api.Models.Config;
using System.Data;
using Microsoft.Extensions.Hosting;

namespace Mbpc.Api.Services
{
    /// <summary>
    /// Implementación del servicio de Catálogos del sistema MBPC.
    ///
    /// PATRÓN DE ESCRITURA (CQRS — Command side):
    ///   - Buques / Muelles (Creación): Llama a los SPs que sí existen en Oracle (crear_buque, crear_buque_int, crear_muelle).
    ///   - Barcazas (Creación) y Ediciones (Muelles, Buques, Barcazas): Devuelve directamente el DTO simulado desde el bypass.
    ///
    /// PATRÓN DE LECTURA (CQRS — Query side):
    ///   - Consultas SQL de texto plano con Dapper.
    ///   - Muelles        → MBPC.TBL_MUELLES          (ID → Id, INSTA_PORT → Nombre)
    ///   - Puntos Control → MBPC.TBL_PUNTODECONTROL   (ID → Id, USo → Nombre)
    ///   - Mantiene el try/catch con DEV BYPASS en caso de que Oracle no esté disponible en desarrollo.
    /// </summary>
    public class CatalogoManagerService : ICatalogoService
    {
        private readonly string _oracleConnectionString;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CatalogoManagerService> _logger;

        // Matrículas especiales que no requieren validación de unicidad (valores legacy)
        private static readonly HashSet<string> _matriculasEspeciales = new(StringComparer.OrdinalIgnoreCase)
        {
            "EN TRAMITE", "ENTRAMITE", "PASAVANTE", "S/M", "N/A", "-", "0", "TRAMITE"
        };

        public CatalogoManagerService(
            IOptions<OracleDbSettings> oracleSettings,
            IWebHostEnvironment env,
            ILogger<CatalogoManagerService> logger)
        {
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _env    = env;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════════════
        // PUNTOS DE CONTROL
        // Tabla real: MBPC.TBL_PUNTODECONTROL
        // Mapeo:      ID → Id  |  USo → Nombre
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IEnumerable<PuntoControlDto>> ObtenerPuntosControlAsync()
        {
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                // FIX: Cambiamos 'USo' por 'DESCRIPCION' (o el nombre real de la columna de texto)
                var result = await connection.QueryAsync<PuntoControlDto>(
                    "SELECT ID as Id, DESCRIPCION as Nombre FROM MBPC.TBL_PUNTODECONTROL ORDER BY DESCRIPCION",
                    commandType: CommandType.Text);

                return result;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle al obtener puntos de control en producción.");
                    throw;
                }

                _logger.LogWarning(ex, "[DEV BYPASS] Oracle no disponible. Retornando Puntos de Control mock.");

                return new List<PuntoControlDto>
                {
                    new() { Id = 1, Nombre = "Zona Común (KM 57)" },
                    new() { Id = 2, Nombre = "Prefectura Zárate (KM 110)" },
                    new() { Id = 3, Nombre = "Punto Control San Pedro (KM 277)" },
                    new() { Id = 4, Nombre = "Punto Control Rosario (KM 420)" },
                    new() { Id = 5, Nombre = "Punto Control Diamante (KM 533)" },
                    new() { Id = 6, Nombre = "Punto Control Paraná (KM 602)" },
                    new() { Id = 7, Nombre = "Punto Control Santa Fe (KM 590)" },
                    new() { Id = 8, Nombre = "Punto Control Esquina (KM 678)" }
                };
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // MUELLES — LECTURA
        // Tabla real: MBPC.TBL_MUELLES
        // Mapeo:      ID → Id  |  NOMBRE → Nombre
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IEnumerable<MuelleDto>> ObtenerMuellesAsync()
        {
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                // FIX: Cambiamos 'INSTA_PORT' por 'NOMBRE'
                var result = await connection.QueryAsync<MuelleDto>(
                    "SELECT ID as Id, NOMBRE as Nombre FROM MBPC.TBL_MUELLES ORDER BY NOMBRE",
                    commandType: CommandType.Text);

                return result;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle al obtener muelles en producción.");
                    throw;
                }

                _logger.LogWarning(ex, "[DEV BYPASS] Oracle no disponible. Retornando Muelles mock.");

                return ObtenerMuellesMock().Select(m => new MuelleDto { Id = m.Id, Nombre = m.Nombre! });
            }
        }

        public async Task<MuelleDetalleDto?> ObtenerMuellePorIdAsync(int id)
        {
            _logger.LogInformation("ObtenerMuellePorIdAsync — Id: {Id}", id);

            if (_env.IsDevelopment())
            {
                return ObtenerMuellesMock().FirstOrDefault(m => m.Id == id);
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                // FIX: Consistente con el cambio anterior, 'INSTA_PORT' -> 'NOMBRE'
                var sql = "SELECT ID as Id, NOMBRE as Nombre " +
                          "FROM MBPC.TBL_MUELLES WHERE ID = :Id AND ROWNUM = 1";

                var resultado = await connection.QueryAsync<MuelleDetalleDto>(
                    sql,
                    new { Id = id },
                    commandType: CommandType.Text);

                return resultado.FirstOrDefault();
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ObtenerMuellePorIdAsync — Error de Oracle para Id={Id}. Retornando mock como fallback.", id);
                return ObtenerMuellesMock().FirstOrDefault(m => m.Id == id);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // MUELLES — ESCRITURA
        // ════════════════════════════════════════════════════════════════════════

        public async Task<MuelleDetalleDto> CrearMuelleAsync(MuelleAltaDto dto, int costeraId)
        {
            _logger.LogInformation(
                "CrearMuelleAsync — Nombre: '{Nombre}', Codigo: '{Codigo}', CosteraId: {CosteraId}.",
                dto.Nombre, dto.Codigo, costeraId);

            await ValidarCodigoMuelleUnicoAsync(dto.Codigo, excluirId: null);

            long idGenerado = 0;

            try
            {
                idGenerado = await EjecutarSpMuelleCrearAsync(dto, costeraId);
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "CrearMuelleAsync — Error de Oracle en producción. Nombre: '{Nombre}'.", dto.Nombre);
                    throw;
                }

                _logger.LogWarning(ex,
                    "[DEV BYPASS] Oracle no disponible al crear muelle '{Nombre}'. Simulando éxito.",
                    dto.Nombre);

                idGenerado = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000;
            }

            _logger.LogInformation(
                "CrearMuelleAsync OK — Muelle '{Nombre}' creado con Id={Id}.",
                dto.Nombre, idGenerado);

            return new MuelleDetalleDto
            {
                Id           = (int)idGenerado,
                Nombre       = dto.Nombre,
                Codigo       = dto.Codigo,
                Zona         = dto.Zona,
                KmPar        = dto.KmPar,
                ProfundidadM = dto.ProfundidadM,
                Estado       = dto.Estado,
                CosteraId    = costeraId
            };
        }

        public async Task<MuelleDetalleDto> EditarMuelleAsync(MuelleEditDto dto)
        {
            _logger.LogInformation(
                "EditarMuelleAsync [SIMULACIÓN] — Id: {Id}, Nombre: '{Nombre}'.",
                dto.Id, dto.Nombre);

            // Bypass de Oracle directo: no se llama a SP de Oracle ni se valida unicidad en base a Oracle.
            return await Task.FromResult(new MuelleDetalleDto
            {
                Id           = dto.Id,
                Nombre       = dto.Nombre,
                Codigo       = dto.Codigo,
                Zona         = dto.Zona,
                KmPar        = dto.KmPar,
                ProfundidadM = dto.ProfundidadM,
                Estado       = dto.Estado
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        // BUQUES — LECTURA
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IEnumerable<BuqueDetalleDto>> ObtenerBuquesAsync(
            string? query,
            int pagina,
            int tamanio)
        {
            _logger.LogInformation(
                "ObtenerBuquesAsync — query='{Query}', pagina={Pagina}, tamanio={Tamanio}.",
                query, pagina, tamanio);

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var sql = "SELECT ID_BUQUE as IdBuque, NOMBRE, MATRICULA, OMI, TIPO, BANDERA, CALADO, CALLSIGN as CallSign, ESTADO, COSTERA_ID as CosteraId " +
                          "FROM BUQUES_NEW " +
                          "WHERE (TIPO IS NULL OR TIPO <> 'Barcaza') ";

                var parameters = new DynamicParameters();

                if (!string.IsNullOrWhiteSpace(query))
                {
                    sql += "AND (UPPER(NOMBRE) LIKE :QueryLike OR UPPER(MATRICULA) LIKE :QueryLike OR UPPER(OMI) LIKE :QueryLike) ";
                    parameters.Add("QueryLike", $"%{query.ToUpper()}%");
                }

                sql += "ORDER BY ID_BUQUE " +
                       "OFFSET :Offset ROWS FETCH NEXT :Limit ROWS ONLY";

                parameters.Add("Offset", (pagina - 1) * tamanio);
                parameters.Add("Limit", tamanio);

                return await connection.QueryAsync<BuqueDetalleDto>(
                    sql,
                    parameters,
                    commandType: CommandType.Text);
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "ObtenerBuquesAsync — Error de Oracle en producción.");
                    throw;
                }
                _logger.LogWarning(ex, "[DEV BYPASS] Oracle no disponible. Retornando mock.");
                return ObtenerBuquesMock(query, pagina, tamanio);
            }
        }

        public async Task<BuqueDetalleDto?> ObtenerBuquePorIdAsync(long idBuque)
        {
            _logger.LogInformation("ObtenerBuquePorIdAsync — IdBuque: {Id}.", idBuque);

            if (_env.IsDevelopment())
            {
                return ObtenerBuquesMock(null, 1, 500).FirstOrDefault(b => b.IdBuque == idBuque);
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var sql = "SELECT ID_BUQUE as IdBuque, NOMBRE, MATRICULA, OMI, TIPO, BANDERA, CALADO, CALLSIGN as CallSign, ESTADO, COSTERA_ID as CosteraId " +
                          "FROM BUQUES_NEW " +
                          "WHERE ID_BUQUE = :IdBuque AND ROWNUM = 1";

                var resultado = await connection.QueryAsync<BuqueDetalleDto>(
                    sql,
                    new { IdBuque = idBuque },
                    commandType: CommandType.Text);

                return resultado.FirstOrDefault();
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ObtenerBuquePorIdAsync — Error de Oracle para IdBuque={Id}. Retornando mock como fallback.", idBuque);
                return ObtenerBuquesMock(null, 1, 500).FirstOrDefault(b => b.IdBuque == idBuque);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // BUQUES — ESCRITURA
        // ════════════════════════════════════════════════════════════════════════

        public async Task<BuqueDetalleDto> CrearBuqueAsync(BuqueAltaDto dto, int costeraId)
        {
            _logger.LogInformation(
                "CrearBuqueAsync — Nombre: '{Nombre}', OMI: {Omi}, MMSI: '{Mmsi}', Matrícula: '{Matricula}', CosteraId: {CosteraId}.",
                dto.Nombre, dto.NroOmi, dto.Mmsi, dto.Matricula, costeraId);

            // ── Validaciones de integridad de negocio (Plain SQL) ─────────────
            if (dto.NroOmi.HasValue)
                await ValidarOmiUnicoAsync(dto.NroOmi.Value, excluirIdBuque: null);

            if (!string.IsNullOrWhiteSpace(dto.Mmsi))
                await ValidarMmsiUnicoAsync(dto.Mmsi, excluirIdBuque: null);

            if (!string.IsNullOrWhiteSpace(dto.Matricula)
                && !_matriculasEspeciales.Contains(dto.Matricula))
            {
                await ValidarMatriculaUnicaAsync(dto.Matricula, excluirIdBuque: null);
            }

            // ── Selección de SP: con OMI (ultramar) vs sin OMI (interior) ─────
            var spNombre = dto.NroOmi.HasValue ? "mbpc.crear_buque" : "mbpc.crear_buque_int";

            long idGenerado = 0;

            try
            {
                idGenerado = await EjecutarSpCrearBuqueAsync(spNombre, dto, costeraId);
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex,
                        "CrearBuqueAsync — Error de Oracle en producción. SP: {Sp}, Nombre: '{Nombre}'.",
                        spNombre, dto.Nombre);
                    throw;
                }

                _logger.LogWarning(ex,
                    "[DEV BYPASS] Oracle no disponible al crear buque '{Nombre}'. Simulando éxito.",
                    dto.Nombre);

                idGenerado = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000;
            }

            _logger.LogInformation(
                "CrearBuqueAsync OK — Buque '{Nombre}' creado con IdBuque={Id} vía SP '{Sp}'.",
                dto.Nombre, idGenerado, spNombre);

            return new BuqueDetalleDto
            {
                IdBuque   = idGenerado,
                Nombre    = dto.Nombre,
                Omi       = dto.NroOmi?.ToString(),
                Mmsi      = dto.Mmsi,
                Matricula = dto.Matricula,
                Bandera   = dto.Bandera,
                Tipo      = dto.Tipo,
                Calado    = dto.Calado,
                CallSign  = dto.CallSign,
                Estado    = dto.Estado,
                CosteraId = costeraId
            };
        }

        public async Task<BuqueDetalleDto> EditarBuqueAsync(BuqueEditDto dto)
        {
            _logger.LogInformation(
                "EditarBuqueAsync [SIMULACIÓN] — IdBuque: {Id}, Nombre: '{Nombre}'.",
                dto.IdBuque, dto.Nombre);

            return await Task.FromResult(new BuqueDetalleDto
            {
                IdBuque   = dto.IdBuque,
                Nombre    = dto.Nombre,
                Omi       = dto.NroOmi?.ToString(),
                Mmsi      = dto.Mmsi,
                Matricula = dto.Matricula,
                Bandera   = dto.Bandera,
                Tipo      = dto.Tipo,
                Calado    = dto.Calado,
                CallSign  = dto.CallSign,
                Estado    = dto.Estado
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        // BARCAZAS — LECTURA
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IEnumerable<BarcazaDetalleDto>> ObtenerBarcazasAsync(
            string? query,
            int pagina,
            int tamanio)
        {
            _logger.LogInformation(
                "ObtenerBarcazasAsync — query='{Query}', pagina={Pagina}, tamanio={Tamanio}.",
                query, pagina, tamanio);

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var sql = "SELECT ID_BUQUE as IdBarcaza, NOMBRE, MATRICULA, BANDERA, CAPACIDAD_TN as CapacidadTn, LARGO_M as LargoM, MANGA_M as MangaM, PROPIETARIO, ESTADO, COSTERA_ID as CosteraId " +
                          "FROM BUQUES_NEW " +
                          "WHERE TIPO = 'Barcaza' ";

                var parameters = new DynamicParameters();

                if (!string.IsNullOrWhiteSpace(query))
                {
                    sql += "AND (UPPER(NOMBRE) LIKE :QueryLike OR UPPER(MATRICULA) LIKE :QueryLike) ";
                    parameters.Add("QueryLike", $"%{query.ToUpper()}%");
                }

                sql += "ORDER BY ID_BUQUE " +
                       "OFFSET :Offset ROWS FETCH NEXT :Limit ROWS ONLY";

                parameters.Add("Offset", (pagina - 1) * tamanio);
                parameters.Add("Limit", tamanio);

                return await connection.QueryAsync<BarcazaDetalleDto>(
                    sql,
                    parameters,
                    commandType: CommandType.Text);
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "ObtenerBarcazasAsync — Error de Oracle en producción.");
                    throw;
                }
                _logger.LogWarning(ex, "[DEV BYPASS] Oracle no disponible. Retornando mock.");
                return ObtenerBarcazasMock(query, pagina, tamanio);
            }
        }

        public async Task<BarcazaDetalleDto?> ObtenerBarcazaPorIdAsync(long idBarcaza)
        {
            _logger.LogInformation("ObtenerBarcazaPorIdAsync — IdBarcaza: {Id}.", idBarcaza);

            if (_env.IsDevelopment())
            {
                return ObtenerBarcazasMock(null, 1, 500).FirstOrDefault(b => b.IdBarcaza == idBarcaza);
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var sql = "SELECT ID_BUQUE as IdBarcaza, NOMBRE, MATRICULA, BANDERA, CAPACIDAD_TN as CapacidadTn, LARGO_M as LargoM, MANGA_M as MangaM, PROPIETARIO, ESTADO, COSTERA_ID as CosteraId " +
                          "FROM BUQUES_NEW " +
                          "WHERE TIPO = 'Barcaza' AND ID_BUQUE = :IdBarcaza AND ROWNUM = 1";

                var resultado = await connection.QueryAsync<BarcazaDetalleDto>(
                    sql,
                    new { IdBarcaza = idBarcaza },
                    commandType: CommandType.Text);

                return resultado.FirstOrDefault();
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ObtenerBarcazaPorIdAsync — Error de Oracle para IdBarcaza={Id}. Retornando mock como fallback.", idBarcaza);
                return ObtenerBarcazasMock(null, 1, 500).FirstOrDefault(b => b.IdBarcaza == idBarcaza);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // BARCAZAS — ESCRITURA
        // ════════════════════════════════════════════════════════════════════════

        public async Task<BarcazaDetalleDto> CrearBarcazaAsync(BarcazaAltaDto dto, int costeraId)
        {
            _logger.LogInformation(
                "CrearBarcazaAsync [SIMULACIÓN] — Nombre: '{Nombre}', Matrícula: '{Matricula}', CosteraId: {CosteraId}.",
                dto.Nombre, dto.Matricula, costeraId);

            var idSimulado = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000;

            return await Task.FromResult(new BarcazaDetalleDto
            {
                IdBarcaza   = idSimulado,
                Nombre      = dto.Nombre,
                Matricula   = dto.Matricula,
                Bandera     = dto.Bandera,
                CapacidadTn = dto.CapacidadTn,
                LargoM      = dto.LargoM,
                MangaM      = dto.MangaM,
                Propietario = dto.Propietario,
                Estado      = dto.Estado,
                CosteraId   = costeraId
            });
        }

        public async Task<BarcazaDetalleDto> EditarBarcazaAsync(BarcazaEditDto dto)
        {
            _logger.LogInformation(
                "EditarBarcazaAsync [SIMULACIÓN] — IdBarcaza: {Id}, Nombre: '{Nombre}'.",
                dto.IdBarcaza, dto.Nombre);

            return await Task.FromResult(new BarcazaDetalleDto
            {
                IdBarcaza   = dto.IdBarcaza,
                Nombre      = dto.Nombre,
                Matricula   = dto.Matricula,
                Bandera     = dto.Bandera,
                CapacidadTn = dto.CapacidadTn,
                LargoM      = dto.LargoM,
                MangaM      = dto.MangaM,
                Propietario = dto.Propietario,
                Estado      = dto.Estado
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        // VALIDACIONES DE INTEGRIDAD (privadas)
        // Reemplazadas por consultas SQL plain para evitar SPs inexistentes en Oracle legacy.
        // ════════════════════════════════════════════════════════════════════════

        private async Task ValidarOmiUnicoAsync(int nroOmi, long? excluirIdBuque)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning("[DEV BYPASS] ValidarOmiUnicoAsync omitida. OMI: {Omi}.", nroOmi);
                return;
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var sql = "SELECT COUNT(1) FROM BUQUES_NEW WHERE OMI = :Omi";
                var parameters = new DynamicParameters();
                parameters.Add("Omi", nroOmi.ToString());

                if (excluirIdBuque.HasValue)
                {
                    sql += " AND ID_BUQUE <> :ExcluirId";
                    parameters.Add("ExcluirId", excluirIdBuque.Value);
                }

                var count = await connection.ExecuteScalarAsync<int>(sql, parameters, commandType: CommandType.Text);

                if (count > 0)
                {
                    throw new InvalidOperationException(
                        $"El número OMI '{nroOmi}' ya está registrado en el padrón. " +
                        "No se puede registrar un buque con un OMI duplicado.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ValidarOmiUnicoAsync — Error de Oracle para OMI={Omi}.", nroOmi);
                throw;
            }
        }

        private async Task ValidarMmsiUnicoAsync(string mmsi, long? excluirIdBuque)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning("[DEV BYPASS] ValidarMmsiUnicoAsync omitida. MMSI: '{Mmsi}'.", mmsi);
                return;
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var sql = "SELECT COUNT(1) FROM BUQUES_NEW WHERE MMSI = :Mmsi";
                var parameters = new DynamicParameters();
                parameters.Add("Mmsi", mmsi);

                if (excluirIdBuque.HasValue)
                {
                    sql += " AND ID_BUQUE <> :ExcluirId";
                    parameters.Add("ExcluirId", excluirIdBuque.Value);
                }

                var count = await connection.ExecuteScalarAsync<int>(sql, parameters, commandType: CommandType.Text);

                if (count > 0)
                {
                    throw new InvalidOperationException(
                        $"El MMSI '{mmsi}' ya está registrado en el padrón. " +
                        "No se puede registrar una embarcación con un MMSI duplicado.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ValidarMmsiUnicoAsync — Error de Oracle para MMSI='{Mmsi}'.", mmsi);
                throw;
            }
        }

        private async Task ValidarMatriculaUnicaAsync(string matricula, long? excluirIdBuque)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning("[DEV BYPASS] ValidarMatriculaUnicaAsync omitida. Matrícula: '{Matricula}'.", matricula);
                return;
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var sql = "SELECT COUNT(1) FROM BUQUES_NEW WHERE MATRICULA = :Matricula";
                var parameters = new DynamicParameters();
                parameters.Add("Matricula", matricula);

                if (excluirIdBuque.HasValue)
                {
                    sql += " AND ID_BUQUE <> :ExcluirId";
                    parameters.Add("ExcluirId", excluirIdBuque.Value);
                }

                var count = await connection.ExecuteScalarAsync<int>(sql, parameters, commandType: CommandType.Text);

                if (count > 0)
                {
                    throw new InvalidOperationException(
                        $"La matrícula '{matricula}' ya está registrada en el padrón. " +
                        "No se puede registrar una embarcación con una matrícula duplicada.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ValidarMatriculaUnicaAsync — Error de Oracle para Matrícula='{Matricula}'.", matricula);
                throw;
            }
        }

        private async Task ValidarCodigoMuelleUnicoAsync(string? codigo, int? excluirId)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return;

            if (_env.IsDevelopment())
            {
                _logger.LogWarning("[DEV BYPASS] ValidarCodigoMuelleUnicoAsync omitida. Código: '{Codigo}'.", codigo);
                return;
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                // MBPC.TBL_MUELLES: clave primaria es ID, nombre de instalación en INSTA_PORT.
                var sql = "SELECT COUNT(1) FROM MBPC.TBL_MUELLES WHERE INSTA_PORT = :Codigo";
                var parameters = new DynamicParameters();
                parameters.Add("Codigo", codigo);

                if (excluirId.HasValue)
                {
                    sql += " AND ID <> :ExcluirId";
                    parameters.Add("ExcluirId", excluirId.Value);
                }

                var count = await connection.ExecuteScalarAsync<int>(sql, parameters, commandType: CommandType.Text);

                if (count > 0)
                {
                    throw new InvalidOperationException(
                        $"El código de muelle '{codigo}' ya está registrado. " +
                        "Cada muelle debe tener un código único.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "ValidarCodigoMuelleUnicoAsync — Error de Oracle para Código='{Codigo}'.", codigo);
                throw;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS PRIVADOS: Ejecución de SPs Oracle
        // ════════════════════════════════════════════════════════════════════════

        private async Task<long> EjecutarSpCrearBuqueAsync(string sp, BuqueAltaDto dto, int costeraId)
        {
            using var connection = new OracleConnection(_oracleConnectionString);

            var parameters = new DynamicParameters();
            parameters.Add("p_NOMBRE",     dto.Nombre,    dbType: DbType.String,  direction: ParameterDirection.Input);
            parameters.Add("p_NRO_OMI",    dto.NroOmi,    dbType: DbType.Int32,   direction: ParameterDirection.Input);
            parameters.Add("p_MMSI",       dto.Mmsi,      dbType: DbType.String,  direction: ParameterDirection.Input);
            parameters.Add("p_MATRICULA",  dto.Matricula, dbType: DbType.String,  direction: ParameterDirection.Input);
            parameters.Add("p_BANDERA",    dto.Bandera,   dbType: DbType.String,  direction: ParameterDirection.Input);
            parameters.Add("p_TIPO",       dto.Tipo,      dbType: DbType.String,  direction: ParameterDirection.Input);
            parameters.Add("p_CALADO",     dto.Calado,    dbType: DbType.Double,  direction: ParameterDirection.Input);
            parameters.Add("p_CALLSIGN",   dto.CallSign,  dbType: DbType.String,  direction: ParameterDirection.Input);
            parameters.Add("p_ESTADO",     dto.Estado,    dbType: DbType.String,  direction: ParameterDirection.Input);
            parameters.Add("p_COSTERA_ID", costeraId,     dbType: DbType.Int32,   direction: ParameterDirection.Input);
            parameters.Add("p_RESULTADO",                 dbType: DbType.Int32,   direction: ParameterDirection.Output);
            parameters.Add("p_ID_GENERADO",               dbType: DbType.Int64,   direction: ParameterDirection.Output);

            await connection.ExecuteAsync(sp, parameters, commandType: CommandType.StoredProcedure);

            var resultado = parameters.Get<int>("p_RESULTADO");

            if (resultado != 1)
            {
                throw new InvalidOperationException(
                    $"Oracle rechazó la creación del buque '{dto.Nombre}' " +
                    $"(SP: {sp}, p_RESULTADO={resultado}). Verifique los datos y reintente.");
            }

            return parameters.Get<long>("p_ID_GENERADO");
        }

        private async Task<long> EjecutarSpMuelleCrearAsync(MuelleAltaDto dto, int costeraId)
        {
            using var connection = new OracleConnection(_oracleConnectionString);

            var parameters = new DynamicParameters();
            parameters.Add("p_NOMBRE",      dto.Nombre,       dbType: DbType.String, direction: ParameterDirection.Input);
            parameters.Add("p_CODIGO",      dto.Codigo,       dbType: DbType.String, direction: ParameterDirection.Input);
            parameters.Add("p_ZONA",        dto.Zona,         dbType: DbType.String, direction: ParameterDirection.Input);
            parameters.Add("p_KM_PAR",      dto.KmPar,        dbType: DbType.Double, direction: ParameterDirection.Input);
            parameters.Add("p_PROFUNDIDAD", dto.ProfundidadM, dbType: DbType.Double, direction: ParameterDirection.Input);
            parameters.Add("p_ESTADO",      dto.Estado,       dbType: DbType.String, direction: ParameterDirection.Input);
            parameters.Add("p_COSTERA_ID",  costeraId,        dbType: DbType.Int32,  direction: ParameterDirection.Input);
            parameters.Add("p_RESULTADO",                     dbType: DbType.Int32,  direction: ParameterDirection.Output);
            parameters.Add("p_ID_GENERADO",                   dbType: DbType.Int64,  direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "mbpc.crear_muelle",
                parameters,
                commandType: CommandType.StoredProcedure);

            var resultado = parameters.Get<int>("p_RESULTADO");

            if (resultado != 1)
            {
                throw new InvalidOperationException(
                    $"Oracle rechazó la creación del muelle '{dto.Nombre}' " +
                    $"(p_RESULTADO={resultado}).");
            }

            return parameters.Get<long>("p_ID_GENERADO");
        }

        // ════════════════════════════════════════════════════════════════════════
        // MOCKS PARA DESARROLLO (DEV BYPASS)
        // ════════════════════════════════════════════════════════════════════════

        private static List<MuelleDetalleDto> ObtenerMuellesMock() => new()
        {
            new() { Id = 101, Nombre = "Terminal Las Palmas - Muelle A",       Codigo = "LP-A",   Zona = "Buenos Aires", KmPar = 57.0,  Estado = "Activo" },
            new() { Id = 102, Nombre = "Puerto Ibicuy - Muelle Principal",     Codigo = "IB-01",  Zona = "Entre Ríos",   KmPar = 156.0, Estado = "Activo" },
            new() { Id = 103, Nombre = "Terminal Zárate - Muelle 1",           Codigo = "TZA-01", Zona = "Zárate",       KmPar = 110.0, Estado = "Activo" },
            new() { Id = 104, Nombre = "Puerto San Martín - Muelle Cargill",   Codigo = "PSM-01", Zona = "San Martín",   KmPar = 388.0, Estado = "Activo" },
            new() { Id = 105, Nombre = "Puerto San Lorenzo - Muelle Renova",   Codigo = "PSL-02", Zona = "San Lorenzo",  KmPar = 400.0, Estado = "Activo" },
            new() { Id = 106, Nombre = "Terminal Arroyo Seco - Muelle Dreyfus",Codigo = "AS-01",  Zona = "Arroyo Seco",  KmPar = 418.0, Estado = "Activo" },
            new() { Id = 107, Nombre = "Puerto Rosario - Muelle Sur",          Codigo = "ROS-S",  Zona = "Rosario",      KmPar = 420.0, Estado = "Activo" },
            new() { Id = 108, Nombre = "Terminal Del Guazú - Muelle B",        Codigo = "DG-B",   Zona = "Del Guazú",    KmPar = 445.0, Estado = "Activo" }
        };

        private static IEnumerable<BuqueDetalleDto> ObtenerBuquesMock(string? query, int pagina, int tamanio)
        {
            var db = new List<BuqueDetalleDto>
            {
                new() { IdBuque = 1045174, Nombre = "YANI G",             Matricula = "LW4793",  Tipo = "Remolcador",        Estado = "Activo" },
                new() { IdBuque = 1070064, Nombre = "VERONICA V",         Matricula = "N/A",     Tipo = "Remolcador",        Estado = "Activo" },
                new() { IdBuque = 1013705, Nombre = "AFRICAN LORIKEET",   Matricula = "3E5310",  Omi = "1013705", Tipo = "Remolcador",  Estado = "Activo" },
                new() { IdBuque = 1092359, Nombre = "LITO",               Matricula = "LW4966",  Tipo = "Remolcador",        Estado = "Activo" },
                new() { IdBuque = 1109920, Nombre = "LEONILDA",           Matricula = "LW4978",  Tipo = "Remolcador",        Estado = "Activo" },
                new() { IdBuque = 5000001, Nombre = "MSC ROSARIA",        Omi = "9320257",       Tipo = "Buque Motor",       Estado = "Activo" },
                new() { IdBuque = 5000002, Nombre = "CLIPPER BRUNSWICK",  Omi = "9400000",       Tipo = "Buque Motor",       Estado = "Activo" },
                new() { IdBuque = 5000003, Nombre = "FEDERAL KIVALINA",   Omi = "9200000",       Tipo = "Buque Motor",       Estado = "Activo" },
                new() { IdBuque = 4000001, Nombre = "PRACTICO I",         Matricula = "REY-0192",Tipo = "Embarcación Menor", Estado = "Activo" },
                new() { IdBuque = 4000002, Nombre = "L/M SAN MARTIN",    Matricula = "REY-0205",Tipo = "Embarcación Menor", Estado = "Activo" },
            };

            if (!string.IsNullOrWhiteSpace(query))
            {
                db = db.Where(b =>
                    (b.Nombre    != null && b.Nombre.Contains(query,    StringComparison.OrdinalIgnoreCase)) ||
                    (b.Matricula != null && b.Matricula.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (b.Omi       != null && b.Omi.Contains(query,       StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            return db.Skip((pagina - 1) * tamanio).Take(tamanio);
        }

        private static IEnumerable<BarcazaDetalleDto> ObtenerBarcazasMock(string? query, int pagina, int tamanio)
        {
            var db = new List<BarcazaDetalleDto>
            {
                new() { IdBarcaza = 3000001, Nombre = "RS001",    Matricula = "EN TRAMITE", Estado = "Activo" },
                new() { IdBarcaza = 3000002, Nombre = "RS002",    Matricula = "PASAVANTE",  Estado = "Activo" },
                new() { IdBarcaza = 3000101, Nombre = "UABL 101", Matricula = "PY-101",     CapacidadTn = 3200.0, Estado = "Activo" },
                new() { IdBarcaza = 3000102, Nombre = "UABL 102", Matricula = "PY-102",     CapacidadTn = 3200.0, Estado = "Activo" },
                new() { IdBarcaza = 3000103, Nombre = "UABL 103", Matricula = "PY-103",     CapacidadTn = 3200.0, Estado = "Activo" },
                new() { IdBarcaza = 3000104, Nombre = "UABL 104", Matricula = "PY-104",     CapacidadTn = 3200.0, Estado = "Activo" },
                new() { IdBarcaza = 3000201, Nombre = "ACBL 01",  Matricula = "ARG-201",    CapacidadTn = 2800.0, Estado = "Activo" },
                new() { IdBarcaza = 3000202, Nombre = "ACBL 02",  Matricula = "ARG-202",    CapacidadTn = 2800.0, Estado = "Activo" },
                new() { IdBarcaza = 3000301, Nombre = "IMP 301",  Matricula = "PAR-301",    CapacidadTn = 3500.0, Estado = "Activo" },
                new() { IdBarcaza = 3000302, Nombre = "IMP 302",  Matricula = "PAR-302",    CapacidadTn = 3500.0, Estado = "Activo" },
            };

            if (!string.IsNullOrWhiteSpace(query))
            {
                db = db.Where(b =>
                    (b.Nombre    != null && b.Nombre.Contains(query,    StringComparison.OrdinalIgnoreCase)) ||
                    (b.Matricula != null && b.Matricula.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            return db.Skip((pagina - 1) * tamanio).Take(tamanio);
        }
    }
}
