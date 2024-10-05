using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.DbContext
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var client = new MongoClient(configuration.GetConnectionString("MongoConnection"));
            _database = client.GetDatabase("TwitchScanDatabase");
            
            var objectSerializer = new ObjectSerializer(type => type.FullName != null && (ObjectSerializer.DefaultAllowedTypes(type) || type.FullName.StartsWith("TwitchScanAPI")));
            BsonSerializer.RegisterSerializer(typeof(object), objectSerializer);
            BsonClassMap.RegisterClassMap<IdEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("IdEntity");
                cm.SetIsRootClass(true); // base class for inheritance
            });

            // TimedEntity
            BsonClassMap.RegisterClassMap<TimedEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("TimedEntity");
            });

            // SentimentAnalysisResult
            BsonClassMap.RegisterClassMap<SentimentAnalysisResult>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("SentimentAnalysisResult");
            });

            // SubscriptionStatisticResult
            BsonClassMap.RegisterClassMap<SubscriptionStatisticResult>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("SubscriptionStatisticResult");
            });

            // ChannelMetrics
            BsonClassMap.RegisterClassMap<ChannelMetrics>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("ChannelMetrics");
            });
            
            // PeakActivityPeriods
            BsonClassMap.RegisterClassMap<PeakActivityPeriods>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("PeakActivityPeriods");
            });
        }

        public IMongoCollection<StatisticHistory> StatisticHistory =>
            _database.GetCollection<StatisticHistory>("StatisticHistory");
    }

}