using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace UKDownloader;

public partial class CheckWindow : Window
{
    private const string CurrentVersion = "2.0.0";
    private const string Repo = "Ukrainian-SCPSL/UKDownloader";
    private const bool Disabled = false;

    public CheckWindow()
    {
        InitializeComponent();

        if (Disabled)
        {
            OpenMainWindow();
            return;
        }

        SimulateCheckAsync();
    }

    private async void SimulateCheckAsync()
    {
        var rnd = new Random();
        var delay = rnd.Next(2000, 5000);
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
                var response = await client.GetAsync("https://api.github.com", HttpCompletionOption.ResponseHeadersRead);
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
            StatusText.Text = "Перевірка на оновлення...";
            Console.WriteLine("➡️ Старт перевірки оновлень...");

            const string GitHubToken = "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"; // замените на свой токен

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UKDownloader");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GitHubToken);

            var json = await client.GetStringAsync($"https://api.github.com/repos/{Repo}/releases");
            using var doc = JsonDocument.Parse(json);

            var latest = doc.RootElement.EnumerateArray().FirstOrDefault(r =>
                r.TryGetProperty("prerelease", out var pre) && !pre.GetBoolean());

            if (latest.ValueKind == JsonValueKind.Undefined)
            {
                Console.WriteLine("❌ Оновлення не знайдено, відкриваємо головне вікно.");
                OpenMainWindow();
                return;
            }

            latestVersionStr = latest.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "0.0.0";
            Console.WriteLine($"ℹ️ Остання версія: {latestVersionStr}");

            if (!Version.TryParse(CurrentVersion.TrimStart('v'), out var currentVersion) ||
                !Version.TryParse(latestVersionStr, out var latestVersion))
            {
                await ShowError($"Не вдалося розпізнати версію: поточна = {CurrentVersion}, остання = {latestVersionStr}");
                return;
            }

            if (currentVersion >= latestVersion)
            {
                Console.WriteLine("✅ Поточна версія новіша або рівна останній. Пропуск оновлення.");
                OpenMainWindow();
                return;
            }

            StatusText.Text = "Скачування нової версії...";
            Console.WriteLine("⬇️ Скачування нової версії...");

            var asset = latest.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(a =>
                    a.TryGetProperty("name", out var nameProp) &&
                    nameProp.GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true &&
                    a.TryGetProperty("browser_download_url", out _));

            if (asset.ValueKind == JsonValueKind.Undefined)
            {
                await ShowError("Не знайдено інсталяційний файл (.exe) в останньому релізі.");
                return;
            }

            downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                await ShowError("URL інсталятора порожній.");
                return;
            }

            Console.WriteLine($"📥 URL для скачування: {downloadUrl}");

            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var cacheDir = Path.Combine(docs, "UKDownloader", "cash");
            Directory.CreateDirectory(cacheDir);

            var exePath = Path.Combine(cacheDir, $"programlatest-{latestVersionStr}.exe");
            Console.WriteLine($"📁 Шлях для збереження: {exePath}");

            using var response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ Відповідь HTTP: {(int)response.StatusCode} {response.ReasonPhrase}");

            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = File.Create(exePath))
            {
                await input.CopyToAsync(output);
            }

            StatusText.Text = "Запуск програми інсталятора...";
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
            Console.WriteLine("❌ Виникла помилка при перевірці оновлення!");
            Console.WriteLine($"⛔ {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);

            var details = $"Помилка під час перевірки оновлення.\n" +
                          $"Поточна версія: {CurrentVersion}\n" +
                          $"Остання: {latestVersionStr ?? "невідома"}\n" +
                          $"URL: {downloadUrl ?? "немає"}\n" +
                          $"Помилка: {ex.Message}";

            await ShowError(details);
            Environment.Exit(0);
        }
    }

    private async Task ShowError(string message)
    {
        await new ErrorWindow(message).ShowDialog(this);
    }

    private void OpenMainWindow()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            new MainWindow().Show();
            Close();
        });
    }

    private static bool IsNewerVersion(string remote, string local)
    {
        if (!Version.TryParse(remote.TrimStart('v', 'V'), out var r))
            return false;

        if (!Version.TryParse(local.TrimStart('v', 'V'), out var l))
            return false;

        return r > l;
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}