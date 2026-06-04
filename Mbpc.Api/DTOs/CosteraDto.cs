using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mbpc.Api.DTOs
{
    public class GeoJsonFeatureCollectionDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "FeatureCollection";

        [JsonPropertyName("features")]
        public List<CosteraDto> Features { get; set; } = new List<CosteraDto>();
    }

    public class CosteraDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Feature";

        [JsonPropertyName("properties")]
        public CosteraPropertiesDto Properties { get; set; } = null!;

        [JsonPropertyName("geometry")]
        public GeoJsonGeometryDto Geometry { get; set; } = null!;
    }

    public class CosteraPropertiesDto
    {
        [JsonPropertyName("costeraId")]
        public int CosteraId { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = null!;
    }

    public class GeoJsonGeometryDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = null!;

        [JsonPropertyName("coordinates")]
        public object? Coordinates { get; set; }
    }
}
