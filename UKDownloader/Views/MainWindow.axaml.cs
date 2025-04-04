using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using YamlDotNet.RepresentationModel;
using static UKDownloader.SelectGameWindow;

namespace UKDownloader;

public partial class MainWindow : Window
{
    private string? _selectedGameTag = null;
    private Dictionary<string, object>? _settings = null;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();

        var version = "2.0.0";
        versionTextBlock.Text = $"Версія програми: v{version}";

        var gameText = this.FindControl<TextBlock>("versionGameTextBlock");
        if (gameText is not null)
            gameText.Text = "Оберіть гру зі списку.";
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, PointerPressedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object? sender, PointerPressedEventArgs e)
    {
        Close();
    }

    private async void Settings_Click(object? sender, PointerPressedEventArgs e)
    {
        var settings = new SettingsWindow();
        await settings.ShowDialog(this);
    }

    private void Discord_Click(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://discord.gg/c67Md8nW5u",
            UseShellExecute = true
        });
    }

    private void GitHub_Click(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Ukrainian-SCPSL",
            UseShellExecute = true
        });
    }

    private void YouTube_Click(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.youtube.com/watch?v=xvFZjo5PgG0",
            UseShellExecute = true
        });
    }

    private async void SelectGame(object? sender, PointerPressedEventArgs e)
    {
        var selectWindow = new SelectGameWindow();
        await selectWindow.ShowDialog(this);

        if (!string.IsNullOrEmpty(selectWindow.SelectedGameTag))
        {
            _selectedGameTag = selectWindow.SelectedGameTag;

            var versionGameTextBlock = this.FindControl<TextBlock>("versionGameTextBlock");
            versionGameTextBlock.Text = SelectGameWindow.AvailableGames
                .FirstOrDefault(g => g.Tag == _selectedGameTag)?.Name ?? "Оберіть гру зі списку.";

            UpdatePathText();
        }
    }

    private async void FolderIcon_Click(object? sender, PointerPressedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedGameTag))
        {
            await new ErrorWindow("Спочатку оберіть гру.").ShowDialog(this);
            return;
        }

        var folder = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            var selectedFolder = folder[0].Path.LocalPath;

            if (_selectedGameTag == "scpsl")
            {
                var index = selectedFolder.IndexOf(@"\Translations", StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    selectedFolder = selectedFolder.Substring(0, index);
                }
            }

            var key = $"{_selectedGameTag}_path";

            if (_settings == null)
                LoadSettings();

            if (!_settings!.TryGetValue("settings", out var settingsObj) || settingsObj is not Dictionary<string, object> innerDict)
            {
                innerDict = new Dictionary<string, object>();
                _settings["settings"] = innerDict;
            }

            innerDict[key] = selectedFolder;
            SaveSettings();

            versionGamePathTextBlock.Text = selectedFolder.Length > 32
                ? selectedFolder[..32] + "..."
                : selectedFolder;
        }
    }

    private void UpdatePathText()
    {
        if (string.IsNullOrEmpty(_selectedGameTag))
        {
            versionGamePathTextBlock.Text = "Оберіть шлях до гри.";
            return;
        }

        if (_settings != null &&
            _settings.TryGetValue("settings", out var inner) &&
            inner is Dictionary<string, object> innerDict &&
            innerDict.TryGetValue($"{_selectedGameTag}_path", out var value))
        {
            var path = value?.ToString();

            if (string.IsNullOrEmpty(path) || path == "null")
            {
                versionGamePathTextBlock.Text = "Оберіть шлях до гри.";
            }
            else
            {
                versionGamePathTextBlock.Text = path!.Length > 32
                    ? path.Substring(0, 32) + "..."
                    : path;
            }
        }
        else
        {
            versionGamePathTextBlock.Text = "Оберіть шлях до гри.";
        }
    }

    private void LoadSettings()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UKDownloader", "settings.yml");
        _settings = new Dictionary<string, object>();

        if (File.Exists(path))
        {
            var yaml = new YamlStream();
            using var reader = new StreamReader(path);
            yaml.Load(reader);

            var root = (YamlMappingNode)yaml.Documents[0].RootNode;

            if (root.Children.TryGetValue(new YamlScalarNode("settings"), out var settingsNode) && settingsNode is YamlMappingNode settingsMap)
            {
                var settingsDict = new Dictionary<string, object>();
                foreach (var entry in settingsMap.Children)
                {
                    settingsDict[entry.Key.ToString()] = entry.Value.ToString().Trim('\'');
                }

                _settings["settings"] = settingsDict;
            }
        }
        else
        {
            _settings["settings"] = new Dictionary<string, object>();
        }
    }

    private void SaveSettings()
    {
        var doc = new YamlMappingNode();
        var settingsNode = new YamlMappingNode();

        if (_settings != null && _settings.TryGetValue("settings", out var inner) && inner is Dictionary<string, object> innerDict)
        {
            foreach (var kv in innerDict)
            {
                var node = new YamlScalarNode(kv.Value?.ToString() ?? "null")
                {
                    Style = YamlDotNet.Core.ScalarStyle.SingleQuoted
                };
                settingsNode.Add(kv.Key, node);
            }
        }

        doc.Add("settings", settingsNode);
        var stream = new YamlStream(new YamlDocument(doc));

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UKDownloader");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "settings.yml");

        using var writer = new StreamWriter(file);
        stream.Save(writer, assignAnchors: false);
    }

    private async void BranchIcon_Click(object? sender, PointerPressedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedGameTag))
        {
            var error = new ErrorWindow("Спочатку оберіть гру.");
            await error.ShowDialog(this);
            return;
        }

        var game = SelectGameWindow.AvailableGames.FirstOrDefault(x => x.Tag == _selectedGameTag);
        if (game is null) return;

        var branchWindow = new SelectBranchWindow(game.RepoUrl, game.HasBeta);
        await branchWindow.ShowDialog(this);

        if (branchWindow.SelectedBranch is not null)
        {
            var versionBranch = branchWindow.SelectedVersion ?? "v1.0.0";

            if (_settings != null &&
                _settings.TryGetValue("settings", out var settingsObj) &&
                settingsObj is Dictionary<string, object> innerDict)
            {
                innerDict[$"{_selectedGameTag}_latestbranch"] = branchWindow.SelectedBranch;
                innerDict[$"{_selectedGameTag}_{branchWindow.SelectedBranch}_version"] = versionBranch;
                SaveSettings();
            }

            var branchNameTextBlock = this.FindControl<TextBlock>("branchNameTextBlock");
            var versionTextBlock = this.FindControl<TextBlock>("versionLocTextBlock");

            branchNameTextBlock.Text = branchWindow.SelectedBranch;

            versionTextBlock.Text = versionBranch == branchWindow.SelectedVersion
                ? versionBranch
                : $"{versionBranch} (застаріла)";
        }
    }

    private async void Download_Click(object? sender, PointerPressedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedGameTag) ||
            !_settings!.TryGetValue("settings", out var settingsObj) ||
            settingsObj is not Dictionary<string, object> innerDict ||
            !innerDict.TryGetValue($"{_selectedGameTag}_path", out var rawPath) ||
            string.IsNullOrEmpty(rawPath?.ToString()) ||
            !innerDict.TryGetValue($"{_selectedGameTag}_latestbranch", out var rawBranch) ||
            string.IsNullOrEmpty(rawBranch?.ToString()))
        {
            await new ErrorWindow("Спочатку оберіть: гру, шлях до гри та гілку.").ShowDialog(this);
            return;
        }

        var branch = rawBranch.ToString();
        var basePath = rawPath.ToString()!;
        var fullPath = Path.Combine(basePath, "Translations");
        Directory.CreateDirectory(fullPath);

        var repo = SelectGameWindow.AvailableGames.First(x => x.Tag == _selectedGameTag);
        var userRepo = repo.RepoUrl.Replace("https://github.com/", "");
        var apiUrl = $"https://api.github.com/repos/{userRepo}/releases";

        const string GitHubToken = "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";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("UKDownloader");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GitHubToken);

        string? zipUrl = null;

        try
        {
            var json = await client.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);

            IEnumerable<JsonElement> releases = doc.RootElement.EnumerateArray();

            JsonElement? release = branch.ToLower() switch
            {
                "latest" => releases.FirstOrDefault(r =>
                    r.TryGetProperty("prerelease", out var pre) && pre.GetBoolean() == false),
                "pre-release" => releases.FirstOrDefault(r =>
                    r.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()),
                _ => null
            };

            if (release is null || release.Value.ValueKind == JsonValueKind.Undefined)
            {
                await new ErrorWindow("Не знайдено відповідний реліз.").ShowDialog(this);
                return;
            }

            var asset = release.Value.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(a => a.GetProperty("name").GetString() == "uk.zip");

            if (asset.ValueKind == JsonValueKind.Undefined)
            {
                await new ErrorWindow("Архів uk.zip не знайдено в релізі.").ShowDialog(this);
                return;
            }

            zipUrl = asset.GetProperty("browser_download_url").GetString();
        }
        catch (Exception ex)
        {
            await new ErrorWindow("Помилка при обробці релізів: " + ex.Message).ShowDialog(this);
            return;
        }

        if (string.IsNullOrWhiteSpace(zipUrl))
        {
            await new ErrorWindow("URL архіву порожній.").ShowDialog(this);
            return;
        }

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var cacheDir = Path.Combine(docs, "UKDownloader", "cash");
        Directory.CreateDirectory(cacheDir);

        var existingZips = Directory.GetFiles(cacheDir, $"{_selectedGameTag}-*.zip");
        var nextIndex = existingZips.Length;
        var zipPath = Path.Combine(cacheDir, $"{_selectedGameTag}-{nextIndex}.zip");

        var progressWindow = new DownloadProgressWindow();
        progressWindow.Show();

        try
        {
            using var response = await client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                progressWindow.Close();
                await new ErrorWindow($"Не вдалося завантажити архів: {response.StatusCode}").ShowDialog(this);
                return;
            }

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

                    progressWindow.SetProgress(percent);
                    progressWindow.SetInfo(readTotal / 1024 + " KB / " + total / 1024 + " KB | Швидкість: " + speed.ToString("0.0") + " KB/s");

                    await Task.Delay(16);
                }

                await output.FlushAsync();
            }

            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length < 512)
            {
                progressWindow.Close();
                await new ErrorWindow("Архів завантажено некоректно або він порожній.").ShowDialog(this);
                return;
            }

            ZipFile.ExtractToDirectory(zipPath, fullPath, true);
            progressWindow.Close();

            var done = new SuccessWindow();
            await done.ShowDialog(this);
        }
        catch (Exception ex)
        {
            progressWindow.Close();
            await new ErrorWindow("Помилка при завантаженні: " + ex.Message).ShowDialog(this);
        }
    }
}