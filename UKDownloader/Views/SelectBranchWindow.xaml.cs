using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace UKDownloader;

public partial class SelectBranchWindow : Window
{
    private string? _selectedBranch;
    private Dictionary<string, string> _latestVersions = new();
    private readonly string _repoUrl;
    private readonly bool _hasBeta;

    public string? SelectedBranch => _selectedBranch;
    public string? SelectedVersion => _selectedBranch != null && _latestVersions.ContainsKey(_selectedBranch) ? _latestVersions[_selectedBranch] : null;

    public SelectBranchWindow(string repoUrl, bool hasBeta)
    {
        InitializeComponent();
        _repoUrl = repoUrl;
        _hasBeta = hasBeta;

        LoadBranches();
    }

    private async void LoadBranches()
    {
        var branches = new List<string> { "Latest" };
        if (_hasBeta)
            branches.Add("Pre-release");

        var tags = await FetchTagsAsync();
        _latestVersions.Clear();

        foreach (var branch in branches)
        {
            var version = tags.FirstOrDefault(t =>
                branch == "Latest"
                    ? !t.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    : t.Contains("pre", StringComparison.OrdinalIgnoreCase)
            ) ?? "unknown";

            _latestVersions[branch] = version;

            var isSelected = branch == _selectedBranch;

            var nameText = new TextBlock
            {
                Text = branch,
                FontFamily = new FontFamily("avares://UKDownloader/Assets/#Montserrat"),
                FontWeight = FontWeight.SemiBold,
                FontSize = 20,
                Foreground = isSelected ? Brushes.Black : Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0)
            };

            var versionText = new TextBlock
            {
                Text = version,
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeight.ExtraLight,
                FontSize = 15,
                Foreground = isSelected ? Brushes.Black : Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 16, 0)
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };

            Grid.SetColumn(versionText, 1);
            grid.Children.Add(nameText);
            grid.Children.Add(versionText);

            var border = new Border
            {
                Background = isSelected ? Brushes.White : new SolidColorBrush(Color.Parse("#474747")),
                CornerRadius = new CornerRadius(10),
                Height = 50,
                Cursor = new Cursor(StandardCursorType.Hand),
                BorderBrush = Brushes.White,
                BorderThickness = isSelected ? new Thickness(2) : new Thickness(0),
                Child = grid
            };

            border.PointerPressed += (_, _) =>
            {
                _selectedBranch = branch;
                UpdateSelection();
            };

            BranchPanel.Children.Add(border);
        }
    }

    private async Task<List<string>> FetchTagsAsync()
    {
        try
        {
            var parts = _repoUrl.Replace("https://github.com/", "").Split('/');
            var apiUrl = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/tags";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UKDownloader");
            var json = await client.GetStringAsync(apiUrl);

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("name").GetString() ?? "")
                .ToList();
        }
        catch
        {
            return new List<string> { "unknown" };
        }
    }

    private void UpdateSelection()
    {
        foreach (var child in BranchPanel.Children)
        {
            if (child is Border border && border.Child is Grid grid)
            {
                var nameBlock = grid.Children.OfType<TextBlock>().First();
                var versionBlock = grid.Children.OfType<TextBlock>().Last();

                var isSelected = nameBlock.Text == _selectedBranch;

                border.Background = isSelected ? Brushes.White : new SolidColorBrush(Color.Parse("#474747"));
                border.BorderThickness = isSelected ? new Thickness(2) : new Thickness(0);
                nameBlock.Foreground = isSelected ? Brushes.Black : Brushes.White;
                versionBlock.Foreground = isSelected ? Brushes.Black : Brushes.White;
            }
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Select_Click(object? sender, PointerPressedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_selectedBranch))
        {
            if (_selectedBranch.Equals("Latest", StringComparison.OrdinalIgnoreCase))
                SelectGameWindow.SelectedBranchType = "Latest";
            else if (_selectedBranch.Equals("Pre-release", StringComparison.OrdinalIgnoreCase))
                SelectGameWindow.SelectedBranchType = "Pre-release";
            else
                SelectGameWindow.SelectedBranchType = string.Empty;

            DiscordPresenceManager.UpdateState("Готується до встановлення 🎯");
            Close();
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        DiscordPresenceManager.UpdateState("Готується до встановлення 🎯");
        Close();
    }
}