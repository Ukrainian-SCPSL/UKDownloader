using System;
using Avalonia;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using System.Collections.Generic;
using YamlDotNet.Serialization.NamingConventions;

namespace UKDownloader;

internal sealed class Program
{
    public const string AppVersion = "2.0.0";

    [STAThread]
    public static void Main(string[] args)
    {
        InitializeSettingsFile();

        if (args.Contains("--background"))
        {
            Console.WriteLine("[UKDownloader] Запуск в фоновому режимі.");
            _ = ScheduleBackgroundLoop();
            Thread.Sleep(Timeout.Infinite);
        }
        else
        {
            Console.WriteLine("[UKDownloader] Запуск з UI.");
            DiscordPresenceManager.Initialize();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void InitializeSettingsFile()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var appFolderPath = Path.Combine(documentsPath, "UKDownloader");
        var settingsPath = Path.Combine(appFolderPath, "settings.yml");
        var cachePath = Path.Combine(appFolderPath, "cash");

        if (!Directory.Exists(appFolderPath))
            Directory.CreateDirectory(appFolderPath);

        if (!Directory.Exists(cachePath))
            Directory.CreateDirectory(cachePath);

        if (!File.Exists(settingsPath))
        {
            var defaultSettings = "settings:\n  scpsl_path: 'null'\n  aul: false";
            File.WriteAllText(settingsPath, defaultSettings);
        }
    }

    private static async Task ScheduleBackgroundLoop()
    {
        await Task.Delay(TimeSpan.FromSeconds(15));
        while (true)
        {
            await BackgroundLoop();
            await Task.Delay(TimeSpan.FromSeconds(45));
        }
    }


    private static async Task BackgroundLoop()
    {
        try
        {
            var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var settingsPath = Path.Combine(docPath, "UKDownloader", "settings.yml");

            var settings = LoadYamlSettings(settingsPath);
            if (settings is null) return;

            if (!settings.TryGetValue("scpsl_path", out var pathObj) || pathObj?.ToString() == "null")
                return;

            var path = pathObj?.ToString()!;
            if (!settings.TryGetValue("scpsl_latestbranch", out var branchObj)) return;
            var branch = branchObj?.ToString()!;
            if (string.IsNullOrWhiteSpace(branch)) return;

            if (!settings.TryGetValue($"scpsl_{branch}_version", out var versionObj)) return;
            var currentVersion = versionObj?.ToString()!;
            if (string.IsNullOrWhiteSpace(currentVersion)) return;

            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UKDownloader");

            var json = await client.GetStringAsync("https://api.github.com/repos/Ukrainian-SCPSL/Ukrainian-language/releases");
            using var doc = JsonDocument.Parse(json);

            var release = doc.RootElement.EnumerateArray().FirstOrDefault(r =>
                r.TryGetProperty("prerelease", out var pre) &&
                (branch == "Latest" && !pre.GetBoolean() || branch == "Pre-release" && pre.GetBoolean()));

            if (release.ValueKind == JsonValueKind.Undefined) return;

            var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v');
            if (string.IsNullOrWhiteSpace(tag)) return;

            if (!Version.TryParse(currentVersion, out var localVer) || !Version.TryParse(tag, out var remoteVer))
                return;

            if (localVer >= remoteVer) return;

            if (!release.TryGetProperty("assets", out var assets)) return;

            var asset = assets.EnumerateArray()
                .FirstOrDefault(a => a.GetProperty("name").GetString() == "uk.zip");

            if (asset.ValueKind == JsonValueKind.Undefined) return;

            var zipUrl = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(zipUrl)) return;

            var translationsPath = Path.Combine(path, "Translations");
            Directory.CreateDirectory(translationsPath);

            var cacheDir = Path.Combine(docPath, "UKDownloader", "cash");
            Directory.CreateDirectory(cacheDir);

            var index = Directory.GetFiles(cacheDir, "scpsl-*.zip").Length;
            var zipPath = Path.Combine(cacheDir, $"scpsl-{index}.zip");

            using var zipResp = await client.GetAsync(zipUrl);
            if (!zipResp.IsSuccessStatusCode) return;

            await using (var input = await zipResp.Content.ReadAsStreamAsync())
            await using (var output = File.Create(zipPath))
                await input.CopyToAsync(output);

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, translationsPath, true);

            settings[$"scpsl_{branch}_version"] = tag;
            SaveYamlSettings(settingsPath, settings);
        }
        catch (Exception ex)
        {
            File.AppendAllText("autoupdate.log", $"[{DateTime.Now}] Помилка: {ex}\n");
        }
    }

    private static Dictionary<string, object>? LoadYamlSettings(string path)
    {
        if (!File.Exists(path)) return null;

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var root = deserializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(yaml);
        return root.TryGetValue("settings", out var settings) ? settings : null;
    }

    private static void SaveYamlSettings(string path, Dictionary<string, object> settings)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var root = new Dictionary<string, object> { { "settings", settings } };
        var yaml = serializer.Serialize(root);
        File.WriteAllText(path, yaml);
    }

    // [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    // private static extern bool AttachConsole(int dwProcessId);

    // private static void AttachConsole()
    // {
    //    const int ATTACH_PARENT_PROCESS = -1;
    //    AttachConsole(ATTACH_PARENT_PROCESS);
    // }
}