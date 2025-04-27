using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace UKDownloader;

public partial class SettingsWindow : Window
{
    public static string SelectedInstallerVersion { get; private set; } = "Latest";
    private bool _autoUpdateEnabled = false;
    private Dictionary<string, object>? _settings;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
        UpdateToggleVisuals();
        UpdateInstallerToggleVisuals();
    }

    private void LoadSettings()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UKDownloader", "settings.yml");

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "settings:\n  scpsl_path: 'null'\n  aul: false\n  ap_selected: 'Latest'");
        }

        var lines = File.ReadAllLines(path);
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("ap_selected:"))
            {
                SelectedInstallerVersion = line.Trim().Split(":")[1].Trim(' ', '\'');
            }
        }
    }

    private void SaveSettings()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UKDownloader", "settings.yml");

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "settings:\n  scpsl_path: 'null'\n  aul: false\n  ap_selected: 'Latest'");
        }

        var lines = File.ReadAllLines(path);
        var updatedAul = false;
        var updatedAp = false;

        using var writer = new StreamWriter(path);

        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("aul:"))
            {
                writer.WriteLine($"  aul: {_autoUpdateEnabled.ToString().ToLower()}");
                updatedAul = true;
            }
            else if (line.Trim().StartsWith("ap_selected:"))
            {
                writer.WriteLine($"  ap_selected: '{SettingsWindow.SelectedInstallerVersion}'");
                updatedAp = true;
            }
            else
            {
                writer.WriteLine(line);
            }
        }

        if (!updatedAul)
            writer.WriteLine($"  aul: {_autoUpdateEnabled.ToString().ToLower()}");

        if (!updatedAp)
            writer.WriteLine($"  ap_selected: '{SettingsWindow.SelectedInstallerVersion}'");
    }

    private void UpdateToggleVisuals()
    {
        if (_autoUpdateEnabled)
        {
            ToggleOn.Background = Brushes.White;
            ToggleOff.Background = new SolidColorBrush(Color.Parse("#474747"));

            ToggleOnText.Foreground = Brushes.Black;
            ToggleOffText.Foreground = Brushes.White;
        }
        else
        {
            ToggleOn.Background = new SolidColorBrush(Color.Parse("#474747"));
            ToggleOff.Background = Brushes.White;

            ToggleOnText.Foreground = Brushes.White;
            ToggleOffText.Foreground = Brushes.Black;
        }
    }

    private void UpdateInstallerToggleVisuals()
    {
        if (SelectedInstallerVersion == "Pre-release")
        {
            ToggleInstallerPre.Background = Brushes.White;
            ToggleInstallerPreText.Foreground = Brushes.Black;

            ToggleInstallerLatest.Background = new SolidColorBrush(Color.Parse("#474747"));
            ToggleInstallerLatestText.Foreground = Brushes.White;
        }
        else
        {
            ToggleInstallerLatest.Background = Brushes.White;
            ToggleInstallerLatestText.Foreground = Brushes.Black;

            ToggleInstallerPre.Background = new SolidColorBrush(Color.Parse("#474747"));
            ToggleInstallerPreText.Foreground = Brushes.White;
        }
    }

    private void EnableAutoUpdate_Click(object? sender, RoutedEventArgs e)
    {
        _autoUpdateEnabled = true;
        UpdateToggleVisuals();
    }

    private void DisableAutoUpdate_Click(object? sender, RoutedEventArgs e)
    {
        _autoUpdateEnabled = false;
        UpdateToggleVisuals();
    }

    private async void Apply_Click(object? sender, PointerPressedEventArgs e)
    {
        SaveSettings();

        await Task.Run(() =>
        {
            var exePath = Environment.ProcessPath ?? "";
            var appName = "UKDownloader";

            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

            if (_autoUpdateEnabled)
                key?.SetValue(appName, $"\"{exePath}\" --background");
            else
                key?.DeleteValue(appName, false);
        });
    }

    private void EnableLatestInstaller_Click(object? sender, RoutedEventArgs e)
    {
        SelectedInstallerVersion = "Latest";
        UpdateInstallerToggleVisuals();
    }

    private void EnablePreInstaller_Click(object? sender, RoutedEventArgs e)
    {
        SelectedInstallerVersion = "Pre-release";
        UpdateInstallerToggleVisuals();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}