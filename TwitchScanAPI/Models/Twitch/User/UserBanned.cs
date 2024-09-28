using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.User
{
    public class UserBanned : UserEntity
    {
        public string BanReason { get; set; }
        
        public UserBanned(string username, string banReason) : base(username)
        {
            BanReason = banReason;
        }
    }
}