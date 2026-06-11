using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Mongo;

namespace Mbpc.Api.Services
{
    public class CosteraManagerService : ICosteraService
    {
        private readonly IMongoCollection<CosteraMongo> _costerasCollection;
        private readonly string _jsonPath;

        public CosteraManagerService(IMongoDatabase database)
        {
            _costerasCollection = database.GetCollection<CosteraMongo>("Costeras");
            
            var localPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "geocercas_costeras.json");
            var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, "Data", "geocercas_costeras.json");
            _jsonPath = File.Exists(localPath) ? localPath : baseDirectoryPath;
        }

        public async Task<IEnumerable<CosteraDto>> ObtenerLimitesJurisdiccionalesAsync()
        {
            if (File.Exists(_jsonPath))
            {
                try
                {
                    var jsonContent = await File.ReadAllTextAsync(_jsonPath);
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var collection = System.Text.Json.JsonSerializer.Deserialize<GeoJsonFeatureCollectionDto>(jsonContent, options);
                    if (collection?.Features != null)
                    {
                        return collection.Features;
                    }
                }
                catch (Exception)
                {
                    // Fallback a MongoDB
                }
            }

            var listaDocumentos = await _costerasCollection.Find(_ => true).ToListAsync();
            return listaDocumentos.Select(MapDocumentToDto).ToList();
        }

        public async Task<CosteraDto?> ObtenerLimitePorCosteraIdAsync(int costeraId)
        {
            if (File.Exists(_jsonPath))
            {
                try
                {
                    var jsonContent = await File.ReadAllTextAsync(_jsonPath);
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var collection = System.Text.Json.JsonSerializer.Deserialize<GeoJsonFeatureCollectionDto>(jsonContent, options);
                    if (collection?.Features != null)
                    {
                        return collection.Features.FirstOrDefault(c => c.Properties.CosteraId == costeraId);
                    }
                }
                catch (Exception)
                {
                    // Fallback a MongoDB
                }
            }

            var documento = await _costerasCollection
                .Find(c => c.CosteraId == costeraId)
                .FirstOrDefaultAsync();

            if (documento == null) return null;

            return MapDocumentToDto(documento);
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
