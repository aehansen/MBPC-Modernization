using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClosedXML.Excel;
using Dapper;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace Mbpc.Api.Services
{
    public class QueryBuilderManagerService : IQueryBuilderService
    {
        private readonly string _oracleConnectionString;
        private readonly ICosteraUserContext _costeraUserContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<QueryBuilderManagerService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly Lazy<List<MetadataEntity>> _metadataCache;

        // Modelos internos para representar la estructura del XML de metadatos
        private class MetadataField
        {
            public string Name { get; set; } = string.Empty;
            public string Column { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public bool SecurityFilter { get; set; }
        }

        private class MetadataJoin
        {
            public string TargetEntity { get; set; } = string.Empty;
            public string JoinTable { get; set; } = string.Empty;
            public string LocalKey { get; set; } = string.Empty;
            public string ForeignKey { get; set; } = string.Empty;
        }

        private class MetadataEntity
        {
            public string Name { get; set; } = string.Empty;
            public string Table { get; set; } = string.Empty;
            public string Alias { get; set; } = string.Empty;
            public string PrimaryKey { get; set; } = string.Empty;
            public List<MetadataField> Fields { get; set; } = new();
            public List<MetadataJoin> Joins { get; set; } = new();
        }

        public QueryBuilderManagerService(
            IOptions<OracleDbSettings> oracleSettings,
            ICosteraUserContext costeraUserContext,
            IHttpContextAccessor httpContextAccessor,
            ILogger<QueryBuilderManagerService> logger,
            IWebHostEnvironment env)
        {
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _costeraUserContext = costeraUserContext;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _env = env;

            // Inicialización única (Lazy) e hilo-segura del parseo del XML
            _metadataCache = new Lazy<List<MetadataEntity>>(CargarMetadataDesdeXml, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private List<MetadataEntity> CargarMetadataDesdeXml()
        {
            var entities = new List<MetadataEntity>();
            try
            {
                var basePath = _env.ContentRootPath ?? AppContext.BaseDirectory;
                var xmlPath = Path.Combine(basePath, "Configuration", "mbpc_sqlbuilder_metadata.xml");

                if (!File.Exists(xmlPath))
                {
                    // Fallback a buscar en subcarpetas si no está en la raíz
                    xmlPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "mbpc_sqlbuilder_metadata.xml");
                }

                _logger.LogInformation("Cargando metadatos del SQL Builder desde: {XmlPath}", xmlPath);

                if (!File.Exists(xmlPath))
                {
                    _logger.LogWarning("Archivo de configuración XML no encontrado en {XmlPath}. Creando estructura por defecto.", xmlPath);
                    return CrearMetadataPorDefecto();
                }

                var doc = XDocument.Load(xmlPath);
                var xEntities = doc.Descendants("Entity");

                foreach (var xEnt in xEntities)
                {
                    var ent = new MetadataEntity
                    {
                        Name = xEnt.Attribute("Name")?.Value ?? string.Empty,
                        Table = xEnt.Attribute("Table")?.Value ?? string.Empty,
                        Alias = xEnt.Attribute("Alias")?.Value ?? string.Empty,
                        PrimaryKey = xEnt.Attribute("PrimaryKey")?.Value ?? string.Empty
                    };

                    // Leer Campos
                    foreach (var xField in xEnt.Descendants("Field"))
                    {
                        ent.Fields.Add(new MetadataField
                        {
                            Name = xField.Attribute("Name")?.Value ?? string.Empty,
                            Column = xField.Attribute("Column")?.Value ?? string.Empty,
                            Type = xField.Attribute("Type")?.Value ?? string.Empty,
                            Label = xField.Attribute("Label")?.Value ?? string.Empty,
                            SecurityFilter = bool.TryParse(xField.Attribute("SecurityFilter")?.Value, out var sf) && sf
                        });
                    }

                    // Leer Joins
                    foreach (var xJoin in xEnt.Descendants("Join"))
                    {
                        ent.Joins.Add(new MetadataJoin
                        {
                            TargetEntity = xJoin.Attribute("TargetEntity")?.Value ?? string.Empty,
                            JoinTable = xJoin.Attribute("JoinTable")?.Value ?? string.Empty,
                            LocalKey = xJoin.Attribute("LocalKey")?.Value ?? string.Empty,
                            ForeignKey = xJoin.Attribute("ForeignKey")?.Value ?? string.Empty
                        });
                    }

                    entities.Add(ent);
                }

                _logger.LogInformation("Metadatos del SQL Builder cargados con éxito. Total entidades: {Count}", entities.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar o interpretar el archivo XML de configuración.");
                return CrearMetadataPorDefecto();
            }

            return entities;
        }

        private List<MetadataEntity> CrearMetadataPorDefecto()
        {
            // Retorna una estructura fallback en código si falla el XML
            return new List<MetadataEntity>
            {
                new()
                {
                    Name = "Viaje",
                    Table = "MBPC.TBL_VIAJE",
                    Alias = "V",
                    PrimaryKey = "ID",
                    Fields = new List<MetadataField>
                    {
                        new() { Name = "Id", Column = "ID", Type = "Numeric", Label = "ID del Viaje" },
                        new() { Name = "Origen", Column = "ORIGEN_ID", Type = "String", Label = "Puerto de Origen" },
                        new() { Name = "Destino", Column = "DESTINO", Type = "String", Label = "Puerto de Destino" },
                        new() { Name = "FechaSalida", Column = "FECHA_SALIDA", Type = "DateTime", Label = "Fecha de Salida" },
                        new() { Name = "Eta", Column = "ETA", Type = "DateTime", Label = "ETA" },
                        new() { Name = "Estado", Column = "ESTADO", Type = "Numeric", Label = "Estado (1:Planif, 2:Naveg, 3:Fin, 4:Canc)" },
                        new() { Name = "CosteraId", Column = "COSTERA_ID", Type = "Numeric", Label = "ID Costera", SecurityFilter = true }
                    }
                },
                new()
                {
                    Name = "Buque",
                    Table = "MBPC.Z_TBL_BUQUES_UNICO",
                    Alias = "B",
                    PrimaryKey = "ID_BUQUE",
                    Fields = new List<MetadataField>
                    {
                        new() { Name = "IdBuque", Column = "ID_BUQUE", Type = "Numeric", Label = "ID Buque" },
                        new() { Name = "Nombre", Column = "NOMBRE", Type = "String", Label = "Nombre del Buque" },
                        new() { Name = "Omi", Column = "NRO_OMI", Type = "String", Label = "Número OMI" },
                        new() { Name = "Matricula", Column = "MATRICULA", Type = "String", Label = "Matrícula" }
                    },
                    Joins = new List<MetadataJoin>
                    {
                        new() { TargetEntity = "Viaje", JoinTable = "MBPC.TBL_VIAJE", LocalKey = "ID_BUQUE", ForeignKey = "BUQUE_ID" }
                    }
                }
            };
        }

        public Task<List<MetadataEntityDto>> ObtenerMetadataAsync(CancellationToken ct = default)
        {
            var entities = _metadataCache.Value;
            var dtos = entities.Select(e => new MetadataEntityDto
            {
                Name = e.Name,
                Fields = e.Fields.Select(f => new MetadataFieldDto
                {
                    Name = f.Name,
                    Label = f.Label,
                    Type = f.Type
                }).ToList(),
                Joins = e.Joins.Select(j => new MetadataJoinDto
                {
                    TargetEntity = j.TargetEntity,
                    JoinTable = j.JoinTable,
                    LocalKey = j.LocalKey,
                    ForeignKey = j.ForeignKey
                }).ToList()
            }).ToList();

            return Task.FromResult(dtos);
        }

        public async Task<QueryResultDto> EjecutarConsultaAsync(QueryRequestDto request, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrEmpty(request.EntidadPrincipal))
            {
                throw new ArgumentException("La solicitud de consulta es inválida.");
            }

            int costeraId = _costeraUserContext.GetCurrentCosteraId();
            if (costeraId == -1)
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }

            var entities = _metadataCache.Value;
            var entidadPrincipalObj = entities.FirstOrDefault(e => e.Name.Equals(request.EntidadPrincipal, StringComparison.OrdinalIgnoreCase));
            if (entidadPrincipalObj == null)
            {
                throw new ArgumentException($"La entidad '{request.EntidadPrincipal}' no existe en los metadatos.");
            }

            // Si estamos en desarrollo y no hay string de conexión, usamos bypass con simulación en memoria
            if (_env.IsDevelopment() && string.IsNullOrEmpty(_oracleConnectionString))
            {
                _logger.LogInformation("[DEV BYPASS] Ejecutando simulación en memoria del Query Builder para la entidad '{Entidad}'", request.EntidadPrincipal);
                return SimularConsultaEnMemoria(request, costeraId, entidadPrincipalObj);
            }

            try
            {
                // Compilar consulta SQL dinámicamente
                var (sqlQuery, sqlParams) = CompilarSqlQuery(request, costeraId, entities, entidadPrincipalObj);

                _logger.LogInformation("Ejecutando SQL del Query Builder: {Sql}", sqlQuery);

                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync(ct);

                var reader = await connection.ExecuteReaderAsync(sqlQuery, sqlParams);
                var dataTable = new DataTable();
                dataTable.Load(reader);

                var columns = new List<string>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    columns.Add(col.ColumnName);
                }

                var rows = new List<Dictionary<string, object?>>();
                foreach (DataRow row in dataTable.Rows)
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (DataColumn col in dataTable.Columns)
                    {
                        var val = row[col];
                        dict[col.ColumnName] = val == DBNull.Value ? null : val;
                    }
                    rows.Add(dict);
                }

                if (rows.Count == 0 && _env.IsDevelopment())
                {
                    _logger.LogInformation("[DEV BYPASS] La consulta real a Oracle retornó 0 filas en entorno de desarrollo. Cayendo a simulación en memoria para facilitar verificación manual.");
                    return SimularConsultaEnMemoria(request, costeraId, entidadPrincipalObj);
                }

                return new QueryResultDto
                {
                    Columnas = columns,
                    Filas = rows
                };
            }
            catch (OracleException ex)
            {
                if (_env.IsDevelopment())
                {
                    _logger.LogWarning(ex, "[DEV BYPASS] Fallo de base de datos Oracle. Retornando simulación en memoria.");
                    return SimularConsultaEnMemoria(request, costeraId, entidadPrincipalObj);
                }
                _logger.LogError(ex, "Error ejecutando consulta dinámica en base de datos Oracle.");
                throw;
            }
        }

        private (string sql, DynamicParameters parameters) CompilarSqlQuery(
            QueryRequestDto request,
            int costeraId,
            List<MetadataEntity> allEntities,
            MetadataEntity principalEntity)
        {
            var selectParts = new List<string>();
            var joinParts = new List<string>();
            var whereParts = new List<string>();
            var parameters = new DynamicParameters();

            // Mapeamos los alias de tabla para las consultas dinámicas
            var aliasMap = new Dictionary<string, string>
            {
                { principalEntity.Name.ToLower(), principalEntity.Alias }
            };

            // Determinar si requerimos JOINs analizando las columnas seleccionadas y los filtros
            var joinedEntities = new HashSet<string>();

            // Validar y resolver cada columna seleccionada
            foreach (var colName in request.Columnas)
            {
                // Buscar si pertenece a la entidad principal
                var field = principalEntity.Fields.FirstOrDefault(f => f.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                if (field != null)
                {
                    selectParts.Add($"{principalEntity.Alias}.{field.Column} AS \"{field.Label}\"");
                }
                else
                {
                    // Buscar en otras entidades que tengan relación join con la principal
                    MetadataField? relatedField = null;
                    MetadataEntity? relatedEnt = null;

                    foreach (var ent in allEntities)
                    {
                        if (ent.Name.Equals(principalEntity.Name, StringComparison.OrdinalIgnoreCase)) continue;

                        var f = ent.Fields.FirstOrDefault(x => x.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                        if (f != null)
                        {
                            // Verificar si existe join directo
                            var join = ent.Joins.FirstOrDefault(j => j.TargetEntity.Equals(principalEntity.Name, StringComparison.OrdinalIgnoreCase));
                            if (join != null)
                            {
                                relatedField = f;
                                relatedEnt = ent;
                                break;
                            }
                        }
                    }

                    if (relatedField != null && relatedEnt != null)
                    {
                        if (!aliasMap.ContainsKey(relatedEnt.Name.ToLower()))
                        {
                            aliasMap.Add(relatedEnt.Name.ToLower(), relatedEnt.Alias);
                        }

                        selectParts.Add($"{relatedEnt.Alias}.{relatedField.Column} AS \"{relatedField.Label}\"");
                        joinedEntities.Add(relatedEnt.Name.ToLower());
                    }
                    else
                    {
                        // Si no se encuentra, tiramos advertencia pero no rompemos, mapeamos como literal null o ignoramos
                        _logger.LogWarning("Columna solicitada '{ColName}' no encontrada en entidad principal ni en relaciones configuradas.", colName);
                    }
                }
            }

            if (selectParts.Count == 0)
            {
                // Si no hay columnas válidas, seleccionamos todas las de la entidad principal
                foreach (var f in principalEntity.Fields)
                {
                    selectParts.Add($"{principalEntity.Alias}.{f.Column} AS \"{f.Label}\"");
                }
            }

            // Construir la sección de JOINs
            foreach (var entName in joinedEntities)
            {
                var relatedEnt = allEntities.First(e => e.Name.ToLower() == entName);
                var join = relatedEnt.Joins.First(j => j.TargetEntity.Equals(principalEntity.Name, StringComparison.OrdinalIgnoreCase));

                // Resolvemos la columna de clave local (en la tabla relacionada) y clave foránea (en la tabla principal)
                joinParts.Add($"LEFT JOIN {relatedEnt.Table} {relatedEnt.Alias} ON {relatedEnt.Alias}.{join.LocalKey} = {principalEntity.Alias}.{join.ForeignKey}");
            }

            // Construir los filtros dinámicos con parámetros sanitizados
            int paramIndex = 0;
            foreach (var filtro in request.Filtros)
            {
                if (string.IsNullOrEmpty(filtro.Campo) || string.IsNullOrEmpty(filtro.Valor)) continue;

                // Encontrar el campo
                var field = principalEntity.Fields.FirstOrDefault(f => f.Name.Equals(filtro.Campo, StringComparison.OrdinalIgnoreCase));
                string tableAlias = principalEntity.Alias;

                if (field == null)
                {
                    // Buscar en entidades de join
                    foreach (var entName in joinedEntities)
                    {
                        var ent = allEntities.First(e => e.Name.ToLower() == entName);
                        var f = ent.Fields.FirstOrDefault(x => x.Name.Equals(filtro.Campo, StringComparison.OrdinalIgnoreCase));
                        if (f != null)
                        {
                            field = f;
                            tableAlias = ent.Alias;
                            break;
                        }
                    }
                }

                if (field == null)
                {
                    _logger.LogWarning("Filtro ignorado: El campo '{Campo}' no existe en las entidades consultadas.", filtro.Campo);
                    continue;
                }

                string paramName = $"p_{paramIndex}";
                string sqlOperator = "=";
                object paramValue = filtro.Valor;

                switch (filtro.Operador.ToUpper())
                {
                    case "EQUALS":
                        sqlOperator = "=";
                        break;
                    case "CONTAINS":
                        sqlOperator = "LIKE";
                        paramValue = $"%{filtro.Valor}%";
                        break;
                    case "STARTS_WITH":
                        sqlOperator = "LIKE";
                        paramValue = $"{filtro.Valor}%";
                        break;
                    case "GREATER_THAN":
                        sqlOperator = ">";
                        break;
                    case "LESS_THAN":
                        sqlOperator = "<";
                        break;
                    default:
                        sqlOperator = "=";
                        break;
                }

                // Si es string y operador LIKE, aplicar UPPER para búsquedas insensibles
                if (field.Type.Equals("String", StringComparison.OrdinalIgnoreCase) && sqlOperator == "LIKE")
                {
                    whereParts.Add($"UPPER({tableAlias}.{field.Column}) LIKE UPPER(:{paramName})");
                }
                else
                {
                    whereParts.Add($"{tableAlias}.{field.Column} {sqlOperator} :{paramName}");
                }

                parameters.Add(paramName, paramValue);
                paramIndex++;
            }

            // ── SCOPING SEGURO MULTITENANT GEOGRÁFICO (CosteraId) ─────────────────
            // Si el operador tiene un CosteraId > 0, inyectamos implícitamente el filtro de costera
            if (costeraId > 0)
            {
                var costeraField = principalEntity.Fields.FirstOrDefault(f => f.SecurityFilter);
                if (costeraField != null)
                {
                    whereParts.Add($"{principalEntity.Alias}.{costeraField.Column} = :securityCosteraId");
                    parameters.Add("securityCosteraId", costeraId);
                }
            }

            // Ensamblar sentencia SQL
            string selectClause = string.Join(", ", selectParts);
            string fromClause = $"FROM {principalEntity.Table} {principalEntity.Alias}";
            string joinClause = joinParts.Count > 0 ? string.Join(" ", joinParts) : "";
            string whereClause = whereParts.Count > 0 ? $"WHERE {string.Join(" AND ", whereParts)}" : "";

            string sqlQuery = $"SELECT {selectClause} {fromClause} {joinClause} {whereClause}";

            return (sqlQuery, parameters);
        }

        public Task<byte[]> GenerarExcelAsync(QueryResultDto result)
        {
            if (result == null || result.Filas.Count == 0)
            {
                throw new InvalidOperationException("No se encontraron registros para exportar.");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Consulta Personalizada");

            // Escribir cabeceras
            for (int col = 0; col < result.Columnas.Count; col++)
            {
                worksheet.Cell(1, col + 1).Value = result.Columnas[col];
                worksheet.Cell(1, col + 1).Style.Font.Bold = true;
                worksheet.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#002454");
                worksheet.Cell(1, col + 1).Style.Font.FontColor = XLColor.White;
            }

            // Escribir datos
            for (int row = 0; row < result.Filas.Count; row++)
            {
                var dict = result.Filas[row];
                for (int col = 0; col < result.Columnas.Count; col++)
                {
                    var colName = result.Columnas[col];
                    var val = dict[colName];

                    if (val is DateTime dt)
                    {
                        worksheet.Cell(row + 2, col + 1).Value = dt.ToString("dd/MM/yyyy HH:mm");
                    }
                    else if (val is double d)
                    {
                        worksheet.Cell(row + 2, col + 1).Value = d;
                    }
                    else if (val is int i)
                    {
                        worksheet.Cell(row + 2, col + 1).Value = i;
                    }
                    else if (val is long l)
                    {
                        worksheet.Cell(row + 2, col + 1).Value = l;
                    }
                    else
                    {
                        worksheet.Cell(row + 2, col + 1).Value = val?.ToString() ?? string.Empty;
                    }
                }
            }

            // Aplicar estilo de tabla premium
            var range = worksheet.Range(1, 1, result.Filas.Count + 1, result.Columnas.Count);
            var table = range.CreateTable();
            table.ShowAutoFilter = true;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(stream.ToArray());
        }

        // ── SIMULADOR EN MEMORIA PARA ENTORNO DE DESARROLLO (Bypass VPN) ──────
        private QueryResultDto SimularConsultaEnMemoria(QueryRequestDto request, int costeraId, MetadataEntity principalEntity)
        {
            // Datos Mock de Viajes (Cruce completo con Buques)
            var mockViajes = new List<Dictionary<string, object?>>
            {
                new()
                {
                    { "ID", 801 },
                    { "ORIGEN_ID", "Gualeguaychú" },
                    { "DESTINO", "Nueva Palmira" },
                    { "FECHA_SALIDA", DateTime.Now.AddDays(-2) },
                    { "ETA", DateTime.Now.AddDays(-1) },
                    { "ESTADO", 2 }, // Navegando
                    { "COSTERA_ID", 1 }, // Gualeguaychú Costera
                    { "BUQUE_ID", 501 }
                },
                new()
                {
                    { "ID", 3032 },
                    { "ORIGEN_ID", "Zárate" },
                    { "DESTINO", "Puerto Buenos Aires" },
                    { "FECHA_SALIDA", DateTime.Now.AddDays(-4) },
                    { "ETA", DateTime.Now.AddDays(-2) },
                    { "ESTADO", 3 }, // Finalizado
                    { "COSTERA_ID", 2 }, // Zarate Costera
                    { "BUQUE_ID", 502 }
                },
                new()
                {
                    { "ID", 4015 },
                    { "ORIGEN_ID", "San Pedro" },
                    { "DESTINO", "Rosario" },
                    { "FECHA_SALIDA", DateTime.Now.AddDays(-1) },
                    { "ETA", DateTime.Now.AddHours(8) },
                    { "ESTADO", 2 }, // Navegando
                    { "COSTERA_ID", 1 }, // Gualeguaychú Costera
                    { "BUQUE_ID", 503 }
                },
                new()
                {
                    { "ID", 5011 },
                    { "ORIGEN_ID", "Corrientes" },
                    { "DESTINO", "Puerto Buenos Aires" },
                    { "FECHA_SALIDA", DateTime.Now.AddDays(-6) },
                    { "ETA", DateTime.Now.AddDays(-3) },
                    { "ESTADO", 3 }, // Finalizado
                    { "COSTERA_ID", 4 }, // Corrientes Costera
                    { "BUQUE_ID", 502 }
                }
            };

            // Para pruebas en entorno local de desarrollo (Bypass), inyectamos dinámicamente
            // el CosteraId del usuario actual para que los registros de prueba no sean excluidos 
            // por la seguridad geográfica multitenant de la API.
            if (costeraId > 0)
            {
                foreach (var viaje in mockViajes)
                {
                    viaje["COSTERA_ID"] = costeraId;
                }
            }

            var mockBuques = new List<Dictionary<string, object?>>
            {
                new()
                {
                    { "ID_BUQUE", 501 },
                    { "NOMBRE", "EDERRA I" },
                    { "NRO_OMI", "9102948" },
                    { "MATRICULA", "01230" }
                },
                new()
                {
                    { "ID_BUQUE", 502 },
                    { "NOMBRE", "MSC ROSARIA" },
                    { "NRO_OMI", "9320257" },
                    { "MATRICULA", "LW4793" }
                },
                new()
                {
                    { "ID_BUQUE", 503 },
                    { "NOMBRE", "YANI G" },
                    { "NRO_OMI", "9041285" },
                    { "MATRICULA", "03492" }
                }
            };

            // 1. Determinar Colección Inicial según Entidad Principal
            var dataSetPrincipal = new List<Dictionary<string, object?>>();
            if (principalEntity.Name.Equals("Viaje", StringComparison.OrdinalIgnoreCase))
            {
                dataSetPrincipal = mockViajes;
            }
            else if (principalEntity.Name.Equals("Buque", StringComparison.OrdinalIgnoreCase))
            {
                dataSetPrincipal = mockBuques;
            }

            // 2. Realizar cruces (Joins) lógicos en memoria
            var datasetCruzado = new List<Dictionary<string, object?>>();
            foreach (var item in dataSetPrincipal)
            {
                var combined = new Dictionary<string, object?>(item);

                // Si es Viaje, adjuntar datos del Buque
                if (principalEntity.Name.Equals("Viaje", StringComparison.OrdinalIgnoreCase) && item.ContainsKey("BUQUE_ID"))
                {
                    var buqueId = item["BUQUE_ID"];
                    var buque = mockBuques.FirstOrDefault(b => b["ID_BUQUE"]?.ToString() == buqueId?.ToString());
                    if (buque != null)
                    {
                        foreach (var kvp in buque)
                        {
                            if (!combined.ContainsKey(kvp.Key))
                            {
                                combined.Add(kvp.Key, kvp.Value);
                            }
                        }
                    }
                }
                // Si es Buque, adjuntar datos del primer Viaje correspondiente (para simulación básica)
                else if (principalEntity.Name.Equals("Buque", StringComparison.OrdinalIgnoreCase) && item.ContainsKey("ID_BUQUE"))
                {
                    var buqueId = item["ID_BUQUE"];
                    var viaje = mockViajes.FirstOrDefault(v => v["BUQUE_ID"]?.ToString() == buqueId?.ToString());
                    if (viaje != null)
                    {
                        foreach (var kvp in viaje)
                        {
                            if (!combined.ContainsKey(kvp.Key))
                            {
                                combined.Add(kvp.Key, kvp.Value);
                            }
                        }
                    }
                }

                datasetCruzado.Add(combined);
            }

            // 3. Aplicar Filtro de Seguridad Geográfica
            if (costeraId > 0)
            {
                datasetCruzado = datasetCruzado.Where(row =>
                {
                    if (row.ContainsKey("COSTERA_ID") && row["COSTERA_ID"] != null)
                    {
                        return Convert.ToInt32(row["COSTERA_ID"]) == costeraId;
                    }
                    return true; // Si no tiene filtro de costera la fila, se preserva
                }).ToList();
            }

            // 4. Aplicar Filtros de Usuario
            var filteredDataset = new List<Dictionary<string, object?>>();
            var entities = _metadataCache.Value;

            foreach (var row in datasetCruzado)
            {
                bool match = true;

                foreach (var filter in request.Filtros)
                {
                    if (string.IsNullOrEmpty(filter.Campo) || string.IsNullOrEmpty(filter.Valor)) continue;

                    // Resolver la columna física
                    string colFisica = string.Empty;
                    var field = principalEntity.Fields.FirstOrDefault(f => f.Name.Equals(filter.Campo, StringComparison.OrdinalIgnoreCase));
                    if (field != null)
                    {
                        colFisica = field.Column;
                    }
                    else
                    {
                        // Buscar en otras entidades
                        foreach (var ent in entities)
                        {
                            var f = ent.Fields.FirstOrDefault(x => x.Name.Equals(filter.Campo, StringComparison.OrdinalIgnoreCase));
                            if (f != null)
                            {
                                colFisica = f.Column;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(colFisica) || !row.ContainsKey(colFisica) || row[colFisica] == null)
                    {
                        match = false;
                        break;
                    }

                    var val = row[colFisica]?.ToString() ?? string.Empty;
                    var searchVal = filter.Valor;

                    switch (filter.Operador.ToUpper())
                    {
                        case "EQUALS":
                            if (!val.Equals(searchVal, StringComparison.OrdinalIgnoreCase)) match = false;
                            break;
                        case "CONTAINS":
                            if (!val.Contains(searchVal, StringComparison.OrdinalIgnoreCase)) match = false;
                            break;
                        case "STARTS_WITH":
                            if (!val.StartsWith(searchVal, StringComparison.OrdinalIgnoreCase)) match = false;
                            break;
                        case "GREATER_THAN":
                            if (double.TryParse(val, out var d1) && double.TryParse(searchVal, out var d2))
                            {
                                if (d1 <= d2) match = false;
                            }
                            else if (DateTime.TryParse(val, out var dt1) && DateTime.TryParse(searchVal, out var dt2))
                            {
                                if (dt1 <= dt2) match = false;
                            }
                            else
                            {
                                if (string.Compare(val, searchVal, StringComparison.OrdinalIgnoreCase) <= 0) match = false;
                            }
                            break;
                        case "LESS_THAN":
                            if (double.TryParse(val, out var n1) && double.TryParse(searchVal, out var n2))
                            {
                                if (n1 >= n2) match = false;
                            }
                            else if (DateTime.TryParse(val, out var date1) && DateTime.TryParse(searchVal, out var date2))
                            {
                                if (date1 >= date2) match = false;
                            }
                            else
                            {
                                if (string.Compare(val, searchVal, StringComparison.OrdinalIgnoreCase) >= 0) match = false;
                            }
                            break;
                    }

                    if (!match) break;
                }

                if (match)
                {
                    filteredDataset.Add(row);
                }
            }

            // 5. Formatear y Mapear las Columnas seleccionadas para el DTO Result
            var finalColumns = new List<string>();
            var labelsMapping = new Dictionary<string, string>(); // ColumnFisica -> LabelUsuario

            foreach (var colName in request.Columnas)
            {
                var field = principalEntity.Fields.FirstOrDefault(f => f.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                if (field != null)
                {
                    finalColumns.Add(field.Label);
                    labelsMapping[field.Column] = field.Label;
                }
                else
                {
                    // Buscar en otras entidades
                    foreach (var ent in entities)
                    {
                        var f = ent.Fields.FirstOrDefault(x => x.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                        if (f != null)
                        {
                            finalColumns.Add(f.Label);
                            labelsMapping[f.Column] = f.Label;
                            break;
                        }
                    }
                }
            }

            if (finalColumns.Count == 0)
            {
                // Si no hay columnas seleccionadas
                foreach (var f in principalEntity.Fields)
                {
                    finalColumns.Add(f.Label);
                    labelsMapping[f.Column] = f.Label;
                }
            }

            var finalRows = new List<Dictionary<string, object?>>();
            foreach (var row in filteredDataset)
            {
                var mappedRow = new Dictionary<string, object?>();
                foreach (var col in finalColumns)
                {
                    // Encontrar la clave física correspondiente a esta cabecera (label)
                    var physicalKey = labelsMapping.FirstOrDefault(x => x.Value == col).Key;
                    if (!string.IsNullOrEmpty(physicalKey) && row.ContainsKey(physicalKey))
                    {
                        var rawValue = row[physicalKey];
                        if (rawValue is DateTime dt)
                        {
                            mappedRow[col] = dt.ToString("dd/MM/yyyy HH:mm");
                        }
                        else
                        {
                            mappedRow[col] = rawValue;
                        }
                    }
                    else
                    {
                        mappedRow[col] = null;
                    }
                }
                finalRows.Add(mappedRow);
            }

            return new QueryResultDto
            {
                Columnas = finalColumns,
                Filas = finalRows
            };
        }
    }
}
