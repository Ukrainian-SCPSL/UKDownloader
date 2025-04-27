using Avalonia.Controls;
using Avalonia.Input;

namespace UKDownloader;

public partial class SuccessWindow : Window
{
    public SuccessWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, PointerPressedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}