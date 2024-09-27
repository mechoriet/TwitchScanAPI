### Rebroadcast Twitch Websocket and Additional Information over REST API

To rebroadcast the Twitch websocket and make additional information available over a REST API, you need to add an `appsettings.json` file for configuration. Below is an example configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*",
  "oauth": "oauth:YourToken",
  "chatName": "YourTwitchBotName"
}
