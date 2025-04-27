using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Media;

namespace UKDownloader;

public partial class DownloadProgressWindow : Window
{
    public DownloadProgressWindow()
    {
        InitializeComponent();
    }

    public void SetProgress(long percent)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressBarFill.Width = ProgressBarBackground.Bounds.Width * percent / 100;
        });
    }

    public void SetInfo(string text)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressInfo.Text = text;
        });
    }
}