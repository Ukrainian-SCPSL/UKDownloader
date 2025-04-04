using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace UKDownloader;

public partial class SelectGameWindow : Window
{
    public static List<GameTag> AvailableGames { get; } = new()
    {
        new GameTag
        {
            Tag = "scpsl",
            Name = "SCP: Secret Laboratory",
            IconFile = "scpsl.png",
            RepoUrl = "https://github.com/Ukrainian-SCPSL/Ukrainian-language",
            HasBeta = true
        }
    };

    private string? _selectedGameTag = null;
    public string? SelectedGameTag => _selectedGameTag;

    public SelectGameWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        GamesPanel.Children.Clear();

        foreach (var game in AvailableGames)
        {
            var bitmap = new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://UKDownloader/Assets/{game.IconFile}")));

            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill
            };

            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            var imageBorder = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Child = image
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(20, 10),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    imageBorder,
                    new TextBlock
                    {
                        Text = game.Name,
                        FontSize = 20,
                        FontFamily = new FontFamily("avares://UKDownloader/Assets/#Montserrat"),
                        FontWeight = FontWeight.SemiBold,
                        Foreground = _selectedGameTag == game.Tag ? Brushes.Black : Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };

            var border = new Border
            {
                Background = new SolidColorBrush(_selectedGameTag == game.Tag ? Colors.White : Color.Parse("#3C3C3C")),
                CornerRadius = new CornerRadius(20),
                Height = 80,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = new Thickness(0, 0, 0, 10),
                Child = panel
            };

            border.PointerPressed += (_, _) =>
            {
                _selectedGameTag = game.Tag;
                OnOpened(EventArgs.Empty);
            };

            GamesPanel.Children.Add(border);
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Close_Click(object? sender, PointerPressedEventArgs e)
    {
        Close();
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_selectedGameTag))
        {
            Close();
        }
    }

    public class GameTag
    {
        public string Tag { get; set; } = "";
        public string Name { get; set; } = "";
        public string IconFile { get; set; } = "";
        public string RepoUrl { get; set; } = "";
        public bool HasBeta { get; set; }
    }
}