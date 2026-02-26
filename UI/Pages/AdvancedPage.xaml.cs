using System.Diagnostics;
using System.IO;
using System.Windows;

namespace SmoothScroller.UI.Pages;

public partial class AdvancedPage : System.Windows.Controls.Page
{
    private bool _loading = true;  // true until OnLoaded completes

    public AdvancedPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        var s = App.Settings.Current;
        TouchpadToggle.IsChecked  = s.DisableTouchpad;
        FrameRateToggle.IsChecked = s.FrameRateAdaptive;
        DpiToggle.IsChecked       = s.ScaleWithDpi;
        DiagToggle.IsChecked      = s.DiagnosticsMode;

        var p = s.GlobalProfile;
        SpeedThresholdSlider.Value = p.SpeedThresholdForSmooth ?? 0.0;
        UpdateLabels();
        _loading = false;
    }

    private void UpdateLabels()
    {
        if (SpeedThresholdLabel is null) return; // Guard: fires during InitializeComponent
        double v = SpeedThresholdSlider.Value;
        SpeedThresholdLabel.Text = v == 0 ? "0 (always smooth)" : $"{v:F0} delta/ms";
    }

    private void SpeedThreshold_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateLabels();
        if (_loading) return;
        App.Settings.Update(s => s.GlobalProfile.SpeedThresholdForSmooth = SpeedThresholdSlider.Value);
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Update(s =>
        {
            s.DisableTouchpad   = TouchpadToggle.IsChecked  == true;
            s.FrameRateAdaptive = FrameRateToggle.IsChecked == true;
            s.ScaleWithDpi      = DpiToggle.IsChecked       == true;
            s.DiagnosticsMode   = DiagToggle.IsChecked      == true;
        });
        App.Logger.IsEnabled = App.Settings.Current.DiagnosticsMode;
    }

    private void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://github.com/SmoothScroller/releases") { UseShellExecute = true }); }
        catch { }
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmoothScroller");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", dir));
    }
}
