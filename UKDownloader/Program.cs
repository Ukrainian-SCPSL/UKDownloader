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
using System.Diagnostics;

namespace UKDownloader;

internal sealed class Program
{
    public const string AppVersion = "2.0.1";

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

            var basePath = pathObj?.ToString()!;
            var fullPath = Path.Combine(basePath, "Translations");
            Directory.CreateDirectory(fullPath);

            if (!settings.TryGetValue("scpsl_latestbranch", out var branchObj)) return;
            var branch = branchObj?.ToString()?.ToLower()!;
            if (string.IsNullOrWhiteSpace(branch)) return;

            var repo = "Ukrainian-SCPSL/Ukrainian-language";
            var apiUrl = $"https://api.github.com/repos/{repo}/releases";

            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UKDownloader");

            var json = await client.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);
            var releases = doc.RootElement.EnumerateArray();

            JsonElement? release = branch switch
            {
                "latest" => releases.FirstOrDefault(r => r.TryGetProperty("prerelease", out var pre) && !pre.GetBoolean()),
                "pre-release" => releases.FirstOrDefault(r =>
                    r.TryGetProperty("prerelease", out var pre) && pre.GetBoolean() ||
                    r.GetProperty("tag_name").GetString()?.Contains("-beta.") == true),
                _ => null
            };

            if (release is null || release.Value.ValueKind == JsonValueKind.Undefined) return;

            var tag = release.Value.GetProperty("tag_name").GetString()?.TrimStart('v');
            if (string.IsNullOrWhiteSpace(tag)) return;

            if (!settings.TryGetValue($"scpsl_{branch}_version", out var versionObj)) return;
            var currentVersion = versionObj?.ToString()!;
            if (!Version.TryParse(currentVersion, out var localVer) || !Version.TryParse(tag, out var remoteVer)) return;
            if (localVer >= remoteVer) return;

            var asset = release.Value.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(a => a.GetProperty("name").GetString() == "uk.zip");

            if (asset.ValueKind == JsonValueKind.Undefined) return;

            var zipUrl = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(zipUrl)) return;

            var cacheDir = Path.Combine(docPath, "UKDownloader", "cash");
            Directory.CreateDirectory(cacheDir);
            var existingZips = Directory.GetFiles(cacheDir, $"scpsl-*.zip");
            var zipPath = Path.Combine(cacheDir, $"scpsl-{existingZips.Length}.zip");

            Console.WriteLine("[UKDownloader] Завантаження uk.zip…");
            using var response = await client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return;

            var total = response.Content.Headers.ContentLength ?? 1;

            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = File.Create(zipPath))
            {
                var buffer = new byte[8192];
                long readTotal = 0;
                var sw = Stopwatch.StartNew();

                while (true)
                {
                    var read = await input.ReadAsync(buffer);
                    if (read == 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    readTotal += read;

                    var percent = readTotal * 100 / total;
                    var speed = readTotal / 1024.0 / sw.Elapsed.TotalSeconds;

                    Console.WriteLine($"⬇️ Прогрес: {percent}% | Швидкість: {speed:0.0} KB/s");
                }

                await output.FlushAsync();
            }

            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length < 512) return;

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, fullPath, true);
            settings[$"scpsl_{branch}_version"] = tag;
            SaveYamlSettings(settingsPath, settings);

            Console.WriteLine("[UKDownloader] ✅ Локалізацію оновлено до версії " + tag);
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