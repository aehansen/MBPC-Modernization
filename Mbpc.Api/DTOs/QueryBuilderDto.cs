using System.Collections.Generic;

namespace Mbpc.Api.DTOs
{
    public class MetadataFieldDto
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Numeric, String, DateTime, etc.
    }

    public class MetadataJoinDto
    {
        public string TargetEntity { get; set; } = string.Empty;
        public string JoinTable { get; set; } = string.Empty;
        public string LocalKey { get; set; } = string.Empty;
        public string ForeignKey { get; set; } = string.Empty;
    }

    public class MetadataEntityDto
    {
        public string Name { get; set; } = string.Empty;
        public List<MetadataFieldDto> Fields { get; set; } = new();
        public List<MetadataJoinDto> Joins { get; set; } = new();
    }

    public class QueryFilterDto
    {
        public string Campo { get; set; } = string.Empty;
        public string Operador { get; set; } = string.Empty; // EQUALS, CONTAINS, GREATER_THAN, LESS_THAN, BETWEEN, etc.
        public string Valor { get; set; } = string.Empty;
    }

    public class QueryRequestDto
    {
        public string EntidadPrincipal { get; set; } = string.Empty;
        public List<string> Columnas { get; set; } = new();
        public List<QueryFilterDto> Filtros { get; set; } = new();
    }

    public class QueryResultDto
    {
        public List<string> Columnas { get; set; } = new();
        public List<Dictionary<string, object?>> Filas { get; set; } = new();
    }
}
