using MongoDB.Bson.Serialization;
using Mbpc.Api.Models.Mongo;

namespace Mbpc.Api.Configuration
{
    public static class MongoMappingConfig
    {
        public static void RegisterMappings()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(ViajeDetalleMongo)))
            {
                BsonClassMap.RegisterClassMap<ViajeDetalleMongo>(cm => 
                { 
                    cm.AutoMap(); 
                    cm.SetIgnoreExtraElements(true); 
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(EtapaMongo)))
            {
                BsonClassMap.RegisterClassMap<EtapaMongo>(cm => 
                { 
                    cm.AutoMap(); 
                    cm.SetIgnoreExtraElements(true); 
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(BarcazaMongo)))
            {
                BsonClassMap.RegisterClassMap<BarcazaMongo>(cm => 
                { 
                    cm.AutoMap(); 
                    cm.SetIgnoreExtraElements(true); 
                });
            }
        }
    }
}
