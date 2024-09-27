using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchScanAPI.Models
{
    public class ChannelError : Error
    {
        public ChannelError(Error error, string channelName) : base(error.ErrorMessage, error.StatusCode)
        {
            ChannelName = channelName;
        }
        public string ChannelName { get; private set; }
    }
}
