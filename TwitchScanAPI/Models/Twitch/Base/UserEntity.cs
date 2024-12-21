namespace TwitchScanAPI.Models.Twitch.Base
{
    public class UserEntity : TimedEntity
    {
        public UserEntity(string username)
        {
            Username = username;
        }

        public string Username { get; set; }
    }
}