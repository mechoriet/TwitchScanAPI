using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch
{
    public class BannedUser : UserEntity
    {
        public string BanReason { get; set; }
        
        public BannedUser(string username, string banReason) : base(username)
        {
            BanReason = banReason;
        }
    }
}