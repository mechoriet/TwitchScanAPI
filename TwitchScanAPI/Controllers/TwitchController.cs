using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Controllers
{
    [Route("[controller]/[action]")]
    public class TwitchController : Controller
    {
        private readonly TwitchChannelManager _twitchStats;
        private readonly MongoDbContext _context;

        public TwitchController(TwitchChannelManager twitchStats, MongoDbContext context)
        {
            _twitchStats = twitchStats;
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult> CreateTestEntry()
        {
            await _context.StatisticHistory.InsertOneAsync(new StatisticHistory("test", 100, 100, null));
            return Ok();
        }
        
        [HttpGet]
        public ActionResult GetAll()
        {
            return Ok(_context.StatisticHistory.Find(_ => true).ToList());
        }
        
        [HttpDelete]
        public ActionResult DeleteAll()
        {
            _context.StatisticHistory.DeleteMany(_ => true);
            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult> Init(string channelName)
        {
            var added = await _twitchStats.Init(channelName);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }
        
        [HttpPost]
        public async Task<ActionResult> InitMultiple([FromBody] string[] channelNames)
        {
            var added = await _twitchStats.InitMultiple(channelNames);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }

        [HttpDelete]
        public ActionResult Remove(string channelName)
        {
            var removed = _twitchStats.Remove(channelName);
            return removed.Error != null
                ? StatusCode(removed.Error.StatusCode, removed)
                : Ok(removed);
        }

        [HttpPost]
        public ActionResult AddTextToObserve(string channelName, string text)
        {
            var added = _twitchStats.AddTextToObserve(channelName, text);
            return added ? Ok() : StatusCode(StatusCodes.Status404NotFound);
        }

        [HttpGet]
        public ActionResult GetInitiatedChannels()
        {
            return Ok(_twitchStats.GetInitiatedChannels());
        }

        [HttpGet]
        public ActionResult GetViewCountHistory(string channelName)
        {
            return Ok(_twitchStats.GetViewCountHistory(channelName));
        }

        [HttpGet]
        public ActionResult GetHistoryByKey(string channelName, Guid key)
        {
            return Ok(_twitchStats.GetHistoryByKey(channelName, key));
        }

        [HttpGet]
        public ActionResult GetUsers(string channelName)
        {
            return Ok(_twitchStats.GetUsers(channelName));
        }

        [HttpGet]
        public async Task<ActionResult> GetPossibleStatistics()
        {
            var stats = await _twitchStats.GetPossibleStatistics();
            return Ok(stats);
        }

        [HttpGet]
        public async Task<ActionResult> GetAllStatistics()
        {
            var stats = await _twitchStats.GetAllStatistics();
            return Ok(stats);
        }

        [HttpGet]
        public async Task<ActionResult> GetChannelStatistics(string channelName)
        {
            var stats = await _twitchStats.GetAllStatistics(channelName);

            return Ok(stats);
        }
    }
}