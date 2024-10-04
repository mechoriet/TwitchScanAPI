using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TwitchScanAPI.Controllers.Annotations;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;

namespace TwitchScanAPI.Controllers
{
    /// <summary>
    /// Controller for database operations. Protected by a master key.
    /// </summary>
    [MasterKey]
    [Route("[controller]/[action]")]
    public class DbController : Controller
    {
        private readonly TwitchChannelManager _twitchStats;
        private readonly IConfiguration _configuration;
        private readonly MongoDbContext _context;
        
        public DbController(TwitchChannelManager twitchStats, IConfiguration configuration, MongoDbContext context)
        {
            _twitchStats = twitchStats;
            _configuration = configuration;
            _context = context;
        }
        
        [HttpGet]
        public async Task<ActionResult> GetDbSize()
        {
            var collection = _context.StatisticHistory;
            var stats = await collection.EstimatedDocumentCountAsync();
            return Ok(stats);
        }

        [HttpPost]
        public async Task<ActionResult> SaveSnapshots()
        {
            await _twitchStats.SaveSnapshotsAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<ActionResult> CleanDb()
        {
            await _context.StatisticHistory.DeleteManyAsync(_ => true);
            return Ok();
        }
        
        [HttpDelete]
        public async Task<ActionResult> DeleteItem(Guid id)
        {
            await _context.StatisticHistory.DeleteOneAsync(x => x.Id == id);
            return Ok();
        }
    }
}