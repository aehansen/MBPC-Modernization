namespace Mbpc.Api.Models.Config
{
    public class OracleDbSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string? TnsAdminPath    { get; set; }
    }
}
