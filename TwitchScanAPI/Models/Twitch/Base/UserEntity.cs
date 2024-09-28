namespace TwitchScanAPI.Models.Twitch.Base
{
    public class UserEntity : TimedEntity
    {
        public string Username { get; set; }
        
        public UserEntity(string username)
        {
            Username = username;
        }
    }
}