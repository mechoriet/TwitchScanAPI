using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.User
{
    public class UserJoined : UserEntity
    {
        public UserJoined(string username) : base(username)
        {
        }
    }
}