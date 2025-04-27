using Avalonia.Controls;
using Avalonia.Input;
using System.Media;

namespace UKDownloader;

public partial class ErrorWindow : Window
{
    public ErrorWindow(string message)
    {
        InitializeComponent();
        ErrorTextBlock.Text = message;
        new SoundPlayer { SoundLocation = "C:\\Windows\\Media\\Windows Error.wav" }.Play();
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
}