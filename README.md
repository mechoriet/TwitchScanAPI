### Rebroadcast Twitch Websocket and Additional Information over REST API

[Live Demo available here - data not persistent](https://dreckbu.de/twitch/)

## Table of Contents

- [Introduction](#introduction)
- [Architecture](#architecture)
- [Installation](#installation)
- [Usage](#usage)
- [Extending with New Statistics](#extending-with-new-statistics)
- [Contributing](#contributing)
- [License](#license)

## Introduction

**Twitch Statistics Analyzer** is an open-source, extensible system designed to collect, analyze, and report comprehensive statistics based on Twitch chat and channel events. Whether you're a streamer looking to gain insights into your community's behavior or a developer aiming to build advanced analytics tools, this project provides a robust foundation tailored for performance and scalability.

## Architecture

The system is built around the following core components:

1. **IStatistic Interface**: Defines the contract for all statistic implementations, ensuring consistency and ease of extension.

    ```csharp
    public interface IStatistic
    {
        string Name { get; }
        object GetResult();
    }
    ```

2. **Statistics Manager**: Discovers and manages all implemented statistics using reflection, handling updates based on Twitch events.

    ```csharp
    public class Statistics
    {
        // Core logic for discovering and updating statistics
    }
    ```

4. **Data Models**: Define various Twitch event models such as `ChannelMessage`, `UserEntity`, `RaidEvent`, etc., used for data processing.

## Installation

### Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later
- [VaderSharp](https://github.com/codingupastorm/vadersharp) for sentiment analysis
- [TwitchLib](https://github.com/TwitchLib/TwitchLib) Twitch Service interaction

### Steps

1. **Clone the Repository**

    ```bash
    git clone https://github.com/LenBanana/TwitchScanAPI.git
    cd twitch-statistics-analyzer
    ```

2. **Add your Twitch Chatbot settings**

    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft": "Warning",
          "Microsoft.Hosting.Lifetime": "Information"
        }
      },
      "AllowedHosts": "*",,
      "ConnectionStrings": {
        "MongoConnection": "mongodb://localhost:27017 (for historical data)"
      },
      "oauth": "your-oauth-token (optional)",
      "refreshToken": "your-refresh-token (needs chatbot, and helix scopes)",
      "clientId": "your-client-id",
      "clientSecret": "your-client-secret",
      "chatName": "your-twitchbot-name"
    }
    ```

2. **Restore Dependencies**

    ```bash
    dotnet restore
    ```

3. **Build the Project**

    ```bash
    dotnet build
    ```

## Usage

### Setting Up

1. **Handling Twitch Events**

    Hook into Twitch events (e.g., new messages, raids, hosts) and update the statistics accordingly.

    ```csharp
    // Example of handling a new chat message
    void OnNewMessage(ChannelMessage message)
    {
        statistics.Update(message);
    }
    ```

2. **Retrieving Statistics**

    Get all statistics or specific ones as needed. This is showcased in the [TwitchConroller](https://github.com/LenBanana/TwitchScanAPI/blob/master/TwitchScanAPI/Controllers/TwitchController.cs)

    ```csharp
    // Retrieve all statistics
    var allStats = statistics.GetAllStatistics();
    
    // Retrieve a specific statistic
    var sentimentStats = statistics.GetStatistic("SentimentAnalysis");
    ```

## Extending with New Statistics

Adding new statistics is straightforward. Implement the `IStatistic` interface and define the necessary logic.

### Steps to Add a New Statistic

1. **Create a New Class Implementing `IStatistic`**

    ```csharp
    public class NewStatistic : IStatistic
    {
        public string Name => "NewStatistic";
    
        public object GetResult()
        {
            // Implement your logic to return the statistic result
        }
    
        public void Update(SomeEventData eventData)
        {
            // Implement your logic to update the statistic based on event data
        }
    }
    ```

    The `Statistics` manager discovers all non-abstract classes implementing `IStatistic` in the assembly automatically via reflection.

2. **Handle Relevant Events**

    Make sure your statistic has `Update` methods that correspond to the events you want to track.

### Example: Top Chatters Statistic

```csharp
public class TopChattersStatistic : IStatistic
{
    public string Name => "TopChatters";

    private readonly ConcurrentDictionary<string, int> _userMessageCounts = new(StringComparer.OrdinalIgnoreCase);
    private const int TopUsersCount = 10;

    public object GetResult()
    {
        return _userMessageCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(TopUsersCount)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public void Update(ChannelMessage message)
    {
        var username = message?.ChatMessage?.Username;
        if (string.IsNullOrWhiteSpace(username)) return;

        _userMessageCounts.AddOrUpdate(username.Trim(), 1, (key, count) => count + 1);
    }
}
```

## Contributing

Contributions are welcome! Please follow these steps to contribute:

1. **Fork the Repository**

    Click the "Fork" button at the top right of the repository page.

2. **Clone Your Fork**

    ```bash
    git clone https://github.com/LenBanana/TwitchScanAPI.git
    cd twitch-statistics-analyzer
    ```

3. **Create a New Branch**

    ```bash
    git checkout -b feature/new-statistic
    ```

4. **Make Your Changes**

    Implement your feature or fix.

5. **Commit Your Changes**

    ```bash
    git commit -m "Add new statistic for XYZ"
    ```

6. **Push to Your Fork**

    ```bash
    git push origin feature/new-statistic
    ```

7. **Open a Pull Request**

    Go to the original repository and open a pull request from your fork.

## License

This project is licensed under the [MIT License](LICENSE).

---
