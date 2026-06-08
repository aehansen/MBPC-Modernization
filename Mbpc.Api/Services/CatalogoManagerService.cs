using Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Options;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Config;
using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Mbpc.Api.Services
{
    public class CatalogoManagerService : ICatalogoService
    {
        private readonly string _oracleConnectionString;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CatalogoManagerService> _logger;

        public CatalogoManagerService(
            IOptions<OracleDbSettings> oracleSettings,
            IWebHostEnvironment env,
            ILogger<CatalogoManagerService> logger)
        {
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _env = env;
            _logger = logger;
        }

        public async Task<IEnumerable<PuntoControlDto>> ObtenerPuntosControlAsync()
        {
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var parameters = new OracleDynamicParameters();
                parameters.Add("vCursor", OracleDbType.RefCursor, ParameterDirection.Output);

                var result = await connection.QueryAsync<PuntoControlDto>(
                    "mbpc.get_puntos_control",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                return result;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle al obtener puntos de control en producción.");
                    throw;
                }

                _logger.LogWarning(ex, "Se activó el Bypass de Desarrollo para Puntos de Control debido a un fallo en la conexión a Oracle.");
                
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

        public async Task<IEnumerable<MuelleDto>> ObtenerMuellesAsync()
        {
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var parameters = new OracleDynamicParameters();
                parameters.Add("vCursor", OracleDbType.RefCursor, ParameterDirection.Output);

                var result = await connection.QueryAsync<MuelleDto>(
                    "mbpc.get_muelles",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                return result;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle al obtener muelles en producción.");
                    throw;
                }

                _logger.LogWarning(ex, "Se activó el Bypass de Desarrollo para Muelles debido a un fallo en la conexión a Oracle.");
                
                return new List<MuelleDto>
                {
                    new() { Id = 101, Nombre = "Terminal Las Palmas - Muelle A" },
                    new() { Id = 102, Nombre = "Puerto Ibicuy - Muelle Principal" },
                    new() { Id = 103, Nombre = "Terminal Zárate - Muelle 1" },
                    new() { Id = 104, Nombre = "Puerto San Martín - Muelle Cargill" },
                    new() { Id = 105, Nombre = "Puerto San Lorenzo - Muelle Renova" },
                    new() { Id = 106, Nombre = "Terminal Arroyo Seco - Muelle Dreyfus" },
                    new() { Id = 107, Nombre = "Puerto Rosario - Muelle Sur" },
                    new() { Id = 108, Nombre = "Terminal Del Guazú - Muelle B" }
                };
            }
        }
    }
}
