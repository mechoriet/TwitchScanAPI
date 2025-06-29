using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using TwitchScanAPI.Controllers.Annotations;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.DbUser;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Controllers
{
    [Route("[controller]/[action]")]
    public class TwitchController(
        TwitchChannelManager twitchChannelManager,
        MongoDbContext context,
        TwitchVodService twitchVodService,
        IHostApplicationLifetime hostApplicationLifetime, 
        IMemoryCache memoryCache)
        : Controller
    {
        private async Task<TwitchLogin?> GetUserFromAccessToken()
        {
            var accessToken = HttpContext.Request.Headers["AccessToken"].ToString();
            return await context.TwitchLogins.Find(x => x.AccessToken == accessToken).FirstOrDefaultAsync();
        }
        
        [HttpPost]
        [MasterKey]
        public ActionResult Stop()
        {
            hostApplicationLifetime.StopApplication();
            return Ok();
        }

        [HttpPost]
        [MasterKey]
        public async Task<ActionResult> Init(string channelName)
        {
            channelName = channelName.Trim().ToLower();
            var added = await twitchChannelManager.Init(channelName);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }

        [HttpPost]
        [MasterKey]
        public async Task<ActionResult> InitMultiple([FromBody] string[] channelNames)
        {
            channelNames = channelNames.Select(x => x.Trim().ToLower()).ToArray();
            var added = await twitchChannelManager.InitMultiple(channelNames);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }


        [HttpPost]
        [AccessToken]
        public async Task<ActionResult> Remove()
        {
            var user = await GetUserFromAccessToken();
            if (user == null) return StatusCode(StatusCodes.Status404NotFound);

            var removed = twitchChannelManager.Remove(user.DisplayName);
            return removed.Error != null
                ? StatusCode(removed.Error.StatusCode, removed)
                : Ok(removed);
        }


        [HttpPost]
        [MasterKey]
        public ActionResult RemoveByChannelName(string channelName)
        {
            channelName = channelName.Trim().ToLower();
            var removed = twitchChannelManager.Remove(channelName);
            return removed.Error != null
                ? StatusCode(removed.Error.StatusCode, removed)
                : Ok(removed);
        }

        [HttpGet]
        [AccessToken]
        public async Task<ActionResult> GetVodsFromChannel(string channelName)
        {
            channelName = channelName.Trim().ToLower();
            var user = await GetUserFromAccessToken();
            if (user == null) return StatusCode(StatusCodes.Status404NotFound);

            if (channelName != user.DisplayName && user.DisplayName.ToLower() != "lenbanana0")
                return StatusCode(StatusCodes.Status401Unauthorized);

            var vods = await twitchVodService.GetVodsFromChannelAsync(channelName);
            return Ok(vods);
        }

        [HttpGet]
        [AccessToken]
        public async Task<ActionResult> GetChatMessagesFromVod(string vodUrlOrId, string date, string channelName,
            int viewCount)
        {
            var user = await GetUserFromAccessToken();
            if (user == null) return StatusCode(StatusCodes.Status404NotFound);

            if (!DateTime.TryParse(date, out var dateTime)) return BadRequest("Invalid date");

            if (channelName != user.DisplayName && user.DisplayName.ToLower() != "lenbanana0")
                return StatusCode(StatusCodes.Status401Unauthorized);

            channelName = channelName.Trim().ToLower();
            var chatMessages = await twitchVodService.GetChatMessagesFromVodAsync(vodUrlOrId, channelName);
            var stats = new StatisticsManager();
            foreach (var chatMessage in chatMessages.Where(chatMessage =>
                         !Variables.BotNames.Contains(chatMessage.ChatMessage.Username,
                             StringComparer.OrdinalIgnoreCase)))
                await stats.Update(chatMessage);

            var result = stats.GetAllStatistics();
            await twitchChannelManager.SaveSnapshotToChannelAsync(channelName, stats, dateTime, viewCount);
            return Ok(new { Statistics = result });
        }

        [HttpPost]
        [MasterKey]
        public ActionResult AddTextToObserve(string channelName, string text)
        {
            channelName = channelName.Trim().ToLower();
            var added = twitchChannelManager.AddTextToObserve(channelName, text);
            return added ? Ok() : StatusCode(StatusCodes.Status404NotFound);
        }

        [HttpGet]
        [MasterKey]
        public ActionResult GetUsers(string channelName)
        {
            channelName = channelName.Trim().ToLower();
            return Ok(twitchChannelManager.GetUsers(channelName));
        }

        [HttpGet]
        [MasterKey]
        public async Task<ActionResult> GetPossibleStatistics()
        {
            var stats = await twitchChannelManager.GetPossibleStatistics();
            return Ok(stats);
        }

        [HttpGet]
        [MasterKey]
        public async Task<ActionResult> GetAllStatistics()
        {
            var stats = await twitchChannelManager.GetAllStatistics();
            return Ok(stats);
        }

        [HttpGet]
        public async Task<ActionResult> GetInitiatedChannels()
        {
            Console.WriteLine("Initiated channels requested by " + HttpContext.Connection.RemoteIpAddress);
    
            var channels = await memoryCache.GetOrCreateAsync("initiated_channels", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                return await twitchChannelManager.GetInitiatedChannels();
            });
    
            return Ok(channels);
        }

        [HttpGet]
        public async Task<ActionResult> GetChannelStatistics(string channelName)
        {
            channelName = channelName.Trim().ToLower();
            Console.WriteLine("Channel statistics requested by " + HttpContext.Connection.RemoteIpAddress);
            var stats = await twitchChannelManager.GetAllStatistics(channelName);
            return Ok(stats);
        }

        [HttpGet]
        public ActionResult GetViewCountHistory(string channelName)
        {
            channelName = channelName.Trim().ToLower();
            Console.WriteLine("View count history requested by " + HttpContext.Connection.RemoteIpAddress);
            return Ok(twitchChannelManager.GetViewCountHistory(channelName));
        }

        [HttpGet]
        public ActionResult GetHistoryByKey(string channelName, string key)
        {
            channelName = channelName.Trim().ToLower();
            Console.WriteLine("History by key requested by " + HttpContext.Connection.RemoteIpAddress);
            return Ok(twitchChannelManager.GetHistoryByKey(channelName, key));
        }

        [HttpGet]
        public ActionResult GetChatHistory(string channelName, string username)
        {
            channelName = channelName.Trim().ToLower();
            Console.WriteLine("Chat history requested by " + HttpContext.Connection.RemoteIpAddress);
            return Ok(twitchChannelManager.GetChatHistory(channelName, username));
        }
    }
}