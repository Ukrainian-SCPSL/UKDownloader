using DiscordRPC;
using DiscordRPC.Logging;

namespace UKDownloader;

public static class DiscordPresenceManager
{
    private static DiscordRpcClient? _client;

    public static void Initialize()
    {
        _client = new DiscordRpcClient("1006640285431894077")
        {
            Logger = new ConsoleLogger() { Level = LogLevel.Warning }
        };

        _client.Initialize();

        _client.SetPresence(new RichPresence
        {
            Details = "Інсталятор української локалізації.",
            State = "Очікування...",
            Assets = new Assets
            {
                LargeImageKey = "logo",
                LargeImageText = "UK Downloader"
            },
            Buttons = new[]
            {
                new Button
                {
                    Label = "Завантажити",
                    Url = "https://github.com/Ukrainian-SCPSL/UKDownloader"
                }
            }
        });
    }

    public static void UpdateState(string state)
    {
        if (_client is null || !_client.IsInitialized) return;

        var current = _client.CurrentPresence;

        _client.SetPresence(new RichPresence
        {
            Details = current.Details,
            State = state,
            Assets = current.Assets,
            Buttons = current.Buttons
        });
    }

    public static void Dispose()
    {
        _client?.Dispose();
    }
}