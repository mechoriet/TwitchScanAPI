using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchScanAPI.Models
{
    public class Error
    {
        public Error(string errorMessage, int statusCode)
        {
            ErrorMessage = errorMessage;
            StatusCode = statusCode;
        }

        public string ErrorMessage { get; set; }
        public int StatusCode { get; set; }
    }
}
