using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.User
{
    public class UserLeft : UserEntity
    {
        public UserLeft(string username) : base(username)
        {
        }
    }
}