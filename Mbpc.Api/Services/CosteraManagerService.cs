using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Mongo;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Mbpc.Api.Services
{
    public class CosteraManagerService : ICosteraService
    {
        private readonly IMongoCollection<CosteraMongo> _costerasCollection;
        private readonly ILogger<CosteraManagerService> _logger;
        private readonly string? _jsonPath;

        public CosteraManagerService(IMongoDatabase database, ILogger<CosteraManagerService> logger)
        {
            _costerasCollection = database.GetCollection<CosteraMongo>("Costeras");
            _logger = logger;
            
            var pathsToTry = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "geocercas_costeras.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Mbpc.Api", "Data", "geocercas_costeras.json"),
                Path.Combine(AppContext.BaseDirectory, "Data", "geocercas_costeras.json"),
                Path.Combine(AppContext.BaseDirectory, "geocercas_costeras.json")
            };

            foreach (var path in pathsToTry)
            {
                if (File.Exists(path))
                {
                    _jsonPath = path;
                    _logger.LogInformation("CosteraManagerService: Cargando geocercas desde archivo: '{Path}'", path);
                    break;
                }
            }

            if (_jsonPath == null)
            {
                _logger.LogWarning("CosteraManagerService: geocercas_costeras.json no encontrado en las rutas especificadas. Se utilizará MongoDB como fallback.");
            }
        }

        private class GeorefCosteraRecord
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
            [JsonPropertyName("etiqueta")]
            public string Etiqueta { get; set; } = null!;
            [JsonPropertyName("lat")]
            public double Lat { get; set; }
            [JsonPropertyName("lng")]
            public double Lng { get; set; }
        }

        private List<CosteraDto> LoadGeorefCosteras()
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "costeras_georreferenciadas.json");
                if (!File.Exists(path))
                {
                    path = Path.Combine(Directory.GetCurrentDirectory(), "Mbpc.Api", "Data", "costeras_georreferenciadas.json");
                }
                if (!File.Exists(path))
                {
                    path = Path.Combine(AppContext.BaseDirectory, "Data", "costeras_georreferenciadas.json");
                }
                if (!File.Exists(path))
                {
                    _logger.LogWarning("LoadGeorefCosteras: costeras_georreferenciadas.json no encontrado.");
                    return new List<CosteraDto>();
                }

                var json = File.ReadAllText(path);
                var records = System.Text.Json.JsonSerializer.Deserialize<List<GeorefCosteraRecord>>(json);
                if (records == null) return new List<CosteraDto>();

                return records.Select(r => new CosteraDto
                {
                    Type = "Feature",
                    Properties = new CosteraPropertiesDto
                    {
                        CosteraId = r.Id,
                        Nombre = r.Etiqueta
                    },
                    Geometry = new GeoJsonGeometryDto
                    {
                        Type = "Point",
                        Coordinates = new double[] { r.Lng, r.Lat } // GeoJSON is [lng, lat]
                    }
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar costeras_georreferenciadas.json");
                return new List<CosteraDto>();
            }
        }

        public Task<IEnumerable<CosteraDto>> ObtenerLimitesJurisdiccionalesAsync()
        {
            var list = LoadGeorefCosteras();
            return Task.FromResult<IEnumerable<CosteraDto>>(list);
        }

        public Task<CosteraDto?> ObtenerLimitePorCosteraIdAsync(int costeraId)
        {
            var list = LoadGeorefCosteras();
            var item = list.FirstOrDefault(c => c.Properties.CosteraId == costeraId);
            return Task.FromResult<CosteraDto?>(item);
        }

        private CosteraDto MapDocumentToDto(CosteraMongo mongoModel)
        {
            object? coordinates = null;
            string type = string.Empty;

            // Casteo seguro de todos los tipos de geometría BSON
            if (mongoModel.Geometria is GeoJsonPolygon<GeoJson2DGeographicCoordinates> polygon)
            {
                type = "Polygon";
                coordinates = polygon.Coordinates.Exterior.Positions
                    .Select(p => new double[] { p.Longitude, p.Latitude }).ToArray();
            }
            else if (mongoModel.Geometria is GeoJsonMultiPolygon<GeoJson2DGeographicCoordinates> multiPolygon)
            {
                type = "MultiPolygon";
                coordinates = multiPolygon.Coordinates.Polygons.Select(poly =>
                    poly.Exterior.Positions.Select(p => new double[] { p.Longitude, p.Latitude }).ToArray()
                ).ToArray();
            }
            else if (mongoModel.Geometria is GeoJsonLineString<GeoJson2DGeographicCoordinates> lineString)
            {
                // Soporte para los datos sembrados (LineString)
                type = "LineString";
                coordinates = lineString.Coordinates.Positions
                    .Select(pos => new double[] { pos.Longitude, pos.Latitude }).ToArray();
            }
            else
            {
                throw new InvalidOperationException($"Tipo de geometría BSON no soportado para la Costera ID {mongoModel.CosteraId}");
            }

            return new CosteraDto
            {
                Type = "Feature",
                Properties = new CosteraPropertiesDto
                {
                    CosteraId = mongoModel.CosteraId,
                    Nombre = mongoModel.Nombre ?? "Desconocida"
                },
                Geometry = new GeoJsonGeometryDto
                {
                    Type = type,
                    Coordinates = coordinates
                }
            };
        }
    }
}
