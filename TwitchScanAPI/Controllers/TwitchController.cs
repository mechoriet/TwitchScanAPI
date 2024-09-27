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

        [HttpGet]
        public ActionResult Init(string channelName)
        {
            var added = _twitchStats.Init(channelName);
            return added.Error != null ? StatusCode(added.Error.StatusCode, added.Error.ErrorMessage) : Ok(added.Result);
        }

        [HttpPost]
        public ActionResult AddTextToObserve(string channelName, string text)
        {
            var added = _twitchStats.AddTextToObserve(channelName, text);
            return added ? Ok() : StatusCode(StatusCodes.Status404NotFound);
        }

        [HttpGet]
        public ActionResult GetUsers(string channelName)
        {
            return Ok(_twitchStats.GetUsers(channelName));
        }

        [HttpGet]
        public ActionResult GetMessages(string channelName)
        {
            return Ok(_twitchStats.GetMessages(channelName));
        }

        [HttpGet]
        public ActionResult GetObservedMessages(string channelName)
        {
            return Ok(_twitchStats.GetObservedMessages(channelName));
        }

        [HttpGet]
        public ActionResult GetElevatedMessages(string channelName)
        {
            return Ok(_twitchStats.GetElevatedMessages(channelName));
        }

        [HttpGet]
        public ActionResult GetTimedOutUsers(string channelName)
        {
            return Ok (_twitchStats.GetTimedOutUsers(channelName));
        }

        [HttpGet]
        public ActionResult GetBannedUsers(string channelName)
        {
            return Ok(_twitchStats.GetBannedUsers(channelName));
        }

        [HttpGet]
        public ActionResult GetClearedMessages(string channelName)
        {
            return Ok(_twitchStats.GetClearedMessages(channelName));
        }
        [HttpGet]
        public ActionResult GetSubscriptions(string channelName)
        {
            return Ok(_twitchStats.GetSubscriptions(channelName));
        }
    }
}
