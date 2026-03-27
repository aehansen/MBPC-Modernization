namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO devuelto por GET /api/viajes/mapa.
    /// Concentra todo lo que el componente MapaAIS.jsx necesita para
    /// renderizar un Feature en ArcGIS: posición actual, origen, destino
    /// y metadatos del buque.
    /// </summary>
    public class MapaViajeDto
    {
        // ── Identidad ────────────────────────────────────────────────────────
        public string Id            { get; set; } = null!;
        public string NombreBuque   { get; set; } = null!;
        public string? Mmsi         { get; set; }
        public int?   Imo           { get; set; }
        public string? Indicativo   { get; set; }   // CallSign

        // ── Posición actual (AIS) ────────────────────────────────────────────
        public double Latitud       { get; set; }
        public double Longitud      { get; set; }
        public double Velocidad     { get; set; }   // SpeedOverGround (kn)
        public double Rumbo         { get; set; }   // CourseOverGround (°)
        public string EstadoNav     { get; set; } = null!;  // NavegationStatusDesc
        public string UltimaActualizacion { get; set; } = null!; // msgTime formateado

        // ── Ruta ─────────────────────────────────────────────────────────────
        public string? Origen       { get; set; }
        public string? Destino      { get; set; }

        /// <summary>
        /// true  → el origen/destino viene de ViajeDetalleMongo (Oracle-backed).
        /// false → viene directamente del registro AIS (puede ser menos preciso).
        /// El frontend puede usar esto para mostrar un ícono de calidad del dato.
        /// </summary>
        public bool TieneDetalleOperativo { get; set; }

        // ── Manifiesto (si existe en ViajeDetalleMongo) ──────────────────────
        public int CantidadBarcazas { get; set; }
        public string? Remolcador   { get; set; }
    }
}
