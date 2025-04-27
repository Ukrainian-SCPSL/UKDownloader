using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace UKDownloader;

public partial class CheckWindow : Window
{
    private const string CurrentVersion = Program.AppVersion;
    private const string Repo = "Ukrainian-SCPSL/UKDownloader";
    private const bool Disabled = false;

    public CheckWindow()
    {
        DiscordPresenceManager.UpdateState("Перевірка на оновлення 🔎");
        InitializeComponent();

        if (Disabled)
        {
            DiscordPresenceManager.UpdateState("Готується до встановлення 🎯");
            OpenMainWindow();
            return;
        }

        SimulateCheckAsync();
    }

    private async void SimulateCheckAsync()
    {
        var delay = new Random().Next(2000, 5000);
        var startTime = DateTime.Now;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += async (_, _) =>
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            var percent = Math.Min(100, elapsed * 100 / delay);
            ProgressBarFill.Width = 3.0 * percent;

            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                var watch = Stopwatch.StartNew();
                var response = await client.GetAsync("https://api.github.com");
                watch.Stop();

                var speed = response.Content.Headers.ContentLength.HasValue
                    ? response.Content.Headers.ContentLength.Value / 1024.0 / watch.Elapsed.TotalSeconds
                    : 0;

                SpeedText.Text = $"Швидкість: {speed:0.0} KB/s";
            }
            catch
            {
                SpeedText.Text = $"Швидкість: ???";
            }
        };
        timer.Start();

        await Task.Delay(delay);
        timer.Stop();

        await CheckForUpdates();
    }

    private async Task CheckForUpdates()
    {
        string? latestVersionStr = null;
        string? downloadUrl = null;

        try
        {
            var selectedBranch = LoadInstallerBranch();
            if (string.IsNullOrWhiteSpace(selectedBranch))
                selectedBranch = SettingsWindow.SelectedInstallerVersion;

            StatusText.Text = $"Перевірка на оновлення...";
            Console.WriteLine($"➡️ Перевірка: {selectedBranch}");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UKDownloader");

            var json = await client.GetStringAsync($"https://api.github.com/repos/{Repo}/releases");
            using var doc = JsonDocument.Parse(json);

            var release = doc.RootElement.EnumerateArray()
                .FirstOrDefault(r =>
                    r.TryGetProperty("prerelease", out var pre) &&
                    (selectedBranch == "Latest" && !pre.GetBoolean() ||
                     selectedBranch == "Pre-release" && pre.GetBoolean()));

            if (release.ValueKind == JsonValueKind.Undefined)
            {
                Console.WriteLine("❌ Реліз не знайдено.");
                OpenMainWindow();
                return;
            }

            latestVersionStr = release.GetProperty("tag_name").GetString()?.TrimStart('v');
            if (!Version.TryParse(CurrentVersion.TrimStart('v'), out var localVer) ||
                !Version.TryParse(latestVersionStr, out var remoteVer))
            {
                await ShowError($"Помилка версії: поточна = {CurrentVersion}, нова = {latestVersionStr}");
                return;
            }

            if (localVer >= remoteVer)
            {
                Console.WriteLine("✅ Оновлення не потрібно.");
                OpenMainWindow();
                return;
            }

            Console.WriteLine("⬇️ Доступне нове оновлення.");

            var asset = release.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(a =>
                    a.GetProperty("name").GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

            if (asset.ValueKind == JsonValueKind.Undefined)
            {
                await ShowError("Інсталятор не знайдено в релізі.");
                return;
            }

            downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                await ShowError("Порожній URL завантаження.");
                return;
            }

            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var cacheDir = Path.Combine(docs, "UKDownloader", "cash");
            Directory.CreateDirectory(cacheDir);

            var exePath = Path.Combine(cacheDir, $"program-{selectedBranch.ToLower()}-{latestVersionStr}.exe");

            using var response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = File.Create(exePath))
                await input.CopyToAsync(output);

            Console.WriteLine("🚀 Запуск інсталятора...");
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Помилка перевірки оновлення:");
            Console.WriteLine(ex);
            await ShowError($"Помилка оновлення.\nПоточна: {CurrentVersion}\nНова: {latestVersionStr ?? "?"}\n{ex.Message}");
            Environment.Exit(0);
        }
    }

    private string? LoadInstallerBranch()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UKDownloader", "settings.yml");

        if (!File.Exists(path)) return null;

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var root = deserializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(yaml);
        return root.TryGetValue("settings", out var settings) &&
               settings.TryGetValue("ap_selected", out var selected)
            ? selected.ToString()
            : null;
    }

    private async Task ShowError(string message)
    {
        await new ErrorWindow(message).ShowDialog(this);
    }

    private void OpenMainWindow()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            DiscordPresenceManager.UpdateState("Готується до встановлення 🎯");
            new MainWindow().Show();
            Close();
        });
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}