using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
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
        }

        public IMongoCollection<StatisticHistory> StatisticHistory =>
            _database.GetCollection<StatisticHistory>("StatisticHistory");
    }

}