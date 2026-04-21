namespace Mbpc.Api.DTOs
{
    public class TipoCargaDto
    {
        public int OracleId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public bool EsPeligrosa { get; set; }
    }
}