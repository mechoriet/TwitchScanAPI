using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TwitchScanAPI.Controllers.Annotations;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;

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
        [MasterKey]
        public async Task<ActionResult> Init(string channelName)
        {
            var added = await _twitchStats.Init(channelName);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }
        
        [HttpPost]
        [MasterKey]
        public async Task<ActionResult> InitMultiple([FromBody] string[] channelNames)
        {
            var added = await _twitchStats.InitMultiple(channelNames);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }

        [HttpPost]
        [AccessToken]
        public async Task<ActionResult> Remove()
        {
            var accessToken = HttpContext.Request.Headers["AccessToken"].ToString();
            // Check if there is a user with the given access token from mongodb using mongodb driver
            var user = await _context.TwitchLogins.Find(x => x.AccessToken == accessToken).FirstOrDefaultAsync();
            if (user == null)
            {
                // To debug get all users from db and log them
                var users = await _context.TwitchLogins.Find(_ => true).ToListAsync();
                Console.WriteLine(users);
                return StatusCode(StatusCodes.Status404NotFound, users);
            }
            
            var removed = _twitchStats.Remove(user.DisplayName);
            return removed.Error != null
                ? StatusCode(removed.Error.StatusCode, removed)
                : Ok(removed);
        }

        [HttpPost]
        [MasterKey]
        public ActionResult AddTextToObserve(string channelName, string text)
        {
            var added = _twitchStats.AddTextToObserve(channelName, text);
            return added ? Ok() : StatusCode(StatusCodes.Status404NotFound);
        }

        [HttpGet]
        [MasterKey]
        public ActionResult GetUsers(string channelName)
        {
            return Ok(_twitchStats.GetUsers(channelName));
        }

        [HttpGet]
        [MasterKey]
        public async Task<ActionResult> GetPossibleStatistics()
        {
            var stats = await _twitchStats.GetPossibleStatistics();
            return Ok(stats);
        }

        [HttpGet]
        [MasterKey]
        public async Task<ActionResult> GetAllStatistics()
        {
            var stats = await _twitchStats.GetAllStatistics();
            return Ok(stats);
        }

        [HttpGet]
        public async Task<ActionResult> GetInitiatedChannels()
        {
            var channels = await _twitchStats.GetInitiatedChannels();
            return Ok(channels);
        }

        [HttpGet]
        public async Task<ActionResult> GetChannelStatistics(string channelName)
        {
            var stats = await _twitchStats.GetAllStatistics(channelName);

            return Ok(stats);
        }

        [HttpGet]
        public ActionResult GetViewCountHistory(string channelName)
        {
            return Ok(_twitchStats.GetViewCountHistory(channelName));
        }

        [HttpGet]
        public ActionResult GetHistoryByKey(string channelName, string key)
        {
            return Ok(_twitchStats.GetHistoryByKey(channelName, key));
        }
    }
}