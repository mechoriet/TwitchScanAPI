using Microsoft.Extensions.Logging;

public static class LoggerFactoryHelper
{
    private static ILoggerFactory Factory { get; }

    static LoggerFactoryHelper()
    {
        Factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    public static ILogger CreateLogger<T>() =>
        Factory.CreateLogger<T>();
}