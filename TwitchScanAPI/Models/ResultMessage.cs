#nullable enable
namespace TwitchScanAPI.Models
{
    public class ResultMessage<T>
    {
        public ResultMessage(T result, Error? error)
        {
            Result = result;
            Error = error;
        }

        public T Result { get; set; }
        public Error? Error { get; set; }
    }
}