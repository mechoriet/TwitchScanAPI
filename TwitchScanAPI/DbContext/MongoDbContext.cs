﻿using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TwitchScanAPI.Models.DbUser;
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

            var objectSerializer = new ObjectSerializer(type =>
                type.FullName != null);
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

            // RaidStatisticResult
            BsonClassMap.RegisterClassMap<RaidStatisticResult>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("RaidStatisticResult");
            });
            
            // CommercialStatisticResult
            BsonClassMap.RegisterClassMap<CommercialStatisticResult>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("CommercialStatisticResult");
            });

            // BotResult
            BsonClassMap.RegisterClassMap<BotResult>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("BotResult");
            });

            // ChatHistory
            BsonClassMap.RegisterClassMap<ChatHistory>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("ChatHistory");
            });
            
            // BanMetrics
            BsonClassMap.RegisterClassMap<BanMetrics>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("BanMetrics");
            });
            
            // TimeoutMetrics
            BsonClassMap.RegisterClassMap<TimeoutMetrics>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminator("TimeoutMetrics");
            });
        }

        public IMongoCollection<StatisticHistory> StatisticHistory =>
            _database.GetCollection<StatisticHistory>("StatisticHistory");

        public IMongoCollection<TwitchLogin> TwitchLogins =>
            _database.GetCollection<TwitchLogin>("TwitchLogins");
    }
}