using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TwitchScanAPI.Data;

namespace TwitchScanAPI.Controllers
{
    [Route("[controller]/[action]")]
    public class TwitchController : Controller
    {
        private readonly TwitchChannelObserver _twitchStats;

        public TwitchController(TwitchChannelObserver twitchStats)
        {
            _twitchStats = twitchStats;
        }

        [HttpPost]
        public async Task<ActionResult> Init(string channelName)
        {
            var added = await _twitchStats.Init(channelName);
            return added.Error != null
                ? StatusCode(added.Error.StatusCode, added.Error.ErrorMessage)
                : Ok(added.Result);
        }

        [HttpDelete]
        public ActionResult Remove(string channelName)
        {
            var removed = _twitchStats.Remove(channelName);
            return removed.Error != null
                ? StatusCode(removed.Error.StatusCode, removed.Error.ErrorMessage)
                : Ok(removed.Result);
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