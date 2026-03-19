namespace Mbpc.Api.Models.Config
{
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string LastMbpcCollectionName { get; set; } = null!;
        public string DetailsMbpcCollectionName { get; set; } = null!;
    }
}