﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public class TwitchController : Controller
    {
        private readonly TwitchChannelManager _twitchChannelManager;
        private readonly MongoDbContext _context;
        private readonly TwitchVodService _twitchVodService;

        public TwitchController(TwitchChannelManager twitchChannelManager, MongoDbContext context,
            TwitchVodService twitchVodService)
        {
            _twitchChannelManager = twitchChannelManager;
            _context = context;
            _twitchVodService = twitchVodService;
        }
        
        private async Task<TwitchLogin?> GetUserFromAccessToken()
        {
            var accessToken = HttpContext.Request.Headers["AccessToken"].ToString();
            return await _context.TwitchLogins.Find(x => x.AccessToken == accessToken).FirstOrDefaultAsync();
        }

        [HttpPost]
        [MasterKey]
        public async Task<ActionResult> Init(string channelName)
        {
            var added = await _twitchChannelManager.Init(channelName);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }

        [HttpPost]
        [MasterKey]
        public async Task<ActionResult> InitMultiple([FromBody] string[] channelNames)
        {
            var added = await _twitchChannelManager.InitMultiple(channelNames);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added)
                : Ok(added);
        }

        
        [HttpPost]
        [MasterKey]
        public async Task<ActionResult> Remove()
        {
            var user = await GetUserFromAccessToken();
            if (user == null)
            {
                return StatusCode(StatusCodes.Status404NotFound);
            }

            var removed = _twitchChannelManager.Remove(user.DisplayName);
            return removed.Error != null
                ? StatusCode(removed.Error.StatusCode, removed)
                : Ok(removed);
        }

        [HttpGet]
        [AccessToken]
        public async Task<ActionResult> GetVodsFromChannel(string channelName)
        {
            var user = await GetUserFromAccessToken();
            if (user == null)
            {
                return StatusCode(StatusCodes.Status404NotFound);
            }
            
            if (channelName != user.DisplayName && user.DisplayName.ToLower() != "lenbanana0")
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }

            var vods = await _twitchVodService.GetVodsFromChannelAsync(channelName);
            return Ok(vods);
        }

        [HttpGet]
        [AccessToken]
        public async Task<ActionResult> GetChatMessagesFromVod(string vodUrlOrId, string date, string channelName, int viewCount)
        {
            var user = await GetUserFromAccessToken();
            if (user == null)
            {
                return StatusCode(StatusCodes.Status404NotFound);
            }

            if (!DateTime.TryParse(date, out var dateTime))
            {
                return BadRequest("Invalid date");
            }
            
            if (channelName != user.DisplayName && user.DisplayName.ToLower() != "lenbanana0")
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }

            var chatMessages = await _twitchVodService.GetChatMessagesFromVodAsync(vodUrlOrId, channelName);
            var stats = new StatisticsManager();
            foreach (var chatMessage in chatMessages.Where(chatMessage =>
                         !Variables.BotNames.Contains(chatMessage.ChatMessage.Username,
                             StringComparer.OrdinalIgnoreCase)))
            {
                await stats.Update(chatMessage);
            }

            var result = stats.GetAllStatistics();
            await _twitchChannelManager.SaveSnapshotToChannelAsync(channelName, stats, dateTime, viewCount);
            return Ok(new { Statistics = result });
        }

        [HttpPost]
        [MasterKey]
        public ActionResult AddTextToObserve(string channelName, string text)
        {
            var added = _twitchChannelManager.AddTextToObserve(channelName, text);
            return added ? Ok() : StatusCode(StatusCodes.Status404NotFound);
        }

        [HttpGet]
        [MasterKey]
        public ActionResult GetUsers(string channelName)
        {
            return Ok(_twitchChannelManager.GetUsers(channelName));
        }

        [HttpGet]
        [MasterKey]
        public async Task<ActionResult> GetPossibleStatistics()
        {
            var stats = await _twitchChannelManager.GetPossibleStatistics();
            return Ok(stats);
        }

        [HttpGet]
        [MasterKey]
        public async Task<ActionResult> GetAllStatistics()
        {
            var stats = await _twitchChannelManager.GetAllStatistics();
            return Ok(stats);
        }

        [HttpGet]
        public async Task<ActionResult> GetInitiatedChannels()
        {
            Console.WriteLine("Initiated channels requested by " + HttpContext.Connection.RemoteIpAddress);
            var channels = await _twitchChannelManager.GetInitiatedChannels();
            return Ok(channels);
        }

        [HttpGet]
        public async Task<ActionResult> GetChannelStatistics(string channelName)
        {
            Console.WriteLine("Channel statistics requested by " + HttpContext.Connection.RemoteIpAddress);
            var stats = await _twitchChannelManager.GetAllStatistics(channelName);
            return Ok(stats);
        }

        [HttpGet]
        public ActionResult GetViewCountHistory(string channelName)
        {
            Console.WriteLine("View count history requested by " + HttpContext.Connection.RemoteIpAddress);
            return Ok(_twitchChannelManager.GetViewCountHistory(channelName));
        }

        [HttpGet]
        public ActionResult GetHistoryByKey(string channelName, string key)
        {
            Console.WriteLine("History by key requested by " + HttpContext.Connection.RemoteIpAddress);
            return Ok(_twitchChannelManager.GetHistoryByKey(channelName, key));
        }
    }
}