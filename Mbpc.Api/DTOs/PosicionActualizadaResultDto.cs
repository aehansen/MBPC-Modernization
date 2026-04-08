using System;

namespace Mbpc.Api.DTOs
{
    public sealed class PosicionActualizadaResultDto
    {
        public string VesselName { get; init; } = null!;
        public double Latitud { get; init; }
        public double Longitud { get; init; }
        public double VelocidadCalculadaKn { get; init; }
        public double DistanciaRecorridaNM { get; init; }
        public string TracklogId { get; init; } = null!;
    }
}