Rebroadcast Twitch websocket and makes additional information available over a REST API.

Need to add a appsettings.json in order for it to work.
Example:

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
