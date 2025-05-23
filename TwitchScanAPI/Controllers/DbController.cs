﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TwitchScanAPI.Controllers.Annotations;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;

namespace TwitchScanAPI.Controllers
{
    /// <summary>
    ///     Controller for database operations. Protected by a master key.
    /// </summary>
    [MasterKey]
    [Route("[controller]/[action]")]
    public class DbController(TwitchChannelManager twitchStats, MongoDbContext context) : Controller
    {
        [HttpGet]
        public async Task<ActionResult> GetDbSize()
        {
            var collection = context.StatisticHistory;
            var stats = await collection.EstimatedDocumentCountAsync();
            return Ok(stats);
        }

        [HttpPost]
        public async Task<ActionResult> SaveSnapshots()
        {
            await twitchStats.SaveSnapshotsAsync();
            return Ok();
        }
        
        [HttpDelete]
        public async Task<ActionResult> CleanDb()
        {
            await context.StatisticHistory.DeleteManyAsync(_ => true);
            return Ok();
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteItem(Guid id)
        {
            await context.StatisticHistory.DeleteOneAsync(x => x.Id == id);
            return Ok();
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteChannel(string channelName)
        {
            channelName = channelName.Trim().ToLower();
            await context.StatisticHistory.DeleteManyAsync(x => x.UserName == channelName);
            twitchStats.Remove(channelName);
            return Ok();
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteEmptyViewCounts()
        {
            await context.StatisticHistory.DeleteManyAsync(x => x.AverageViewers == 0 || x.PeakViewers == 0);
            return Ok();
        }
    }
}