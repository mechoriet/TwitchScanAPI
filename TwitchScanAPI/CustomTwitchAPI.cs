using Microsoft.Extensions.Configuration;
using TwitchLib.Api;
using TwitchScanAPI.Global;

namespace TwitchScanAPI;

public class CustomTwitchAPI
{
    
    private static CustomTwitchAPI? _instance;
    private readonly TwitchAPI _api = new();

    public CustomTwitchAPI(IConfiguration configuration)
    {
        _api.Settings.ClientId = configuration.GetValue<string>(Variables.TwitchClientId);
        _api.Settings.Secret = configuration.GetValue<string>(Variables.TwitchClientSecret);
    }


    public static CustomTwitchAPI getInstance(IConfiguration configuration)
    {
        if (_instance == null)
        {
            _instance = new CustomTwitchAPI(configuration);
            return _instance;
        }

        return _instance;
    }

    public TwitchAPI getTwitchAPI()
    {
        return _api;
    }
}