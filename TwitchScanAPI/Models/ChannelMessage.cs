using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Models;

namespace TwitchScanAPI.Models
{
    public class ChannelMessage
    {
        public ChannelMessage(string channel, ChatMessage chatMessage)
        {
            Channel = channel;
            ChatMessage = chatMessage;
        }

        public string Channel { get; set; }
        public ChatMessage ChatMessage { get; set; }
        
        public DateTime TimeStamp { get; set; } = DateTime.Now;
    }
}
