using System.Windows;

namespace SmoothScroller.UI.Pages;

public partial class DiagnosticsPage : System.Windows.Controls.Page
{
    public DiagnosticsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        App.Logger.LogEntry += (_, line) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                LogText.Text += line + "\n";
                LogScroller.ScrollToEnd();
            });
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh_Click(this, e);

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LogText.Text = string.Join("\n", App.Logger.ReadLines());
        LogScroller.ScrollToEnd();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        App.Logger.Clear();
        LogText.Text = "";
    }
}
