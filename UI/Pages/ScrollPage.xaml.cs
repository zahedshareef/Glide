using System.Windows;
using System.Windows.Controls;

namespace SmoothScroller.UI.Pages;

public partial class ScrollPage : System.Windows.Controls.Page
{
    private bool _loading = true;  // true until OnLoaded completes — blocks Save/UpdateLabels during InitializeComponent

    public ScrollPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        var p = App.Settings.Current.GlobalProfile;

        StepSizeSlider.Value   = p.StepSize         ?? 120;
        AnimTimeSlider.Value   = p.AnimationTime     ?? 360;
        AccelDeltaSlider.Value = p.AccelerationDelta ?? 70;
        AccelMaxSlider.Value   = p.AccelerationMax   ?? 7.0;
        TailHeadSlider.Value   = p.TailToHeadRatio   ?? 3.0;

        EasingToggle.IsChecked          = p.AnimationEasing      ?? true;
        ShiftHorizToggle.IsChecked      = p.ShiftKeyHorizontal   ?? true;
        HorizSmoothnessToggle.IsChecked = p.HorizontalSmoothness ?? true;
        ReverseToggle.IsChecked         = p.ReverseDirection     ?? false;
        ClickStopToggle.IsChecked       = p.ClickToStop          ?? true;

        UpdateLabels();
        _loading = false;
    }

    private void UpdateLabels()
    {
        // Guard: each Slider fires ValueChanged during InitializeComponent() when its Minimum
        // is set. Labels in *later* CardExpanders are still null at that point.
        if (StepSizeLabel is null || AnimTimeLabel is null ||
            AccelDeltaLabel is null || AccelMaxLabel is null || TailHeadLabel is null) return;

        StepSizeLabel.Text   = $"{(int)StepSizeSlider.Value} px";
        AnimTimeLabel.Text   = $"{(int)AnimTimeSlider.Value} ms";
        AccelDeltaLabel.Text = $"{(int)AccelDeltaSlider.Value} ms";
        AccelMaxLabel.Text   = $"{AccelMaxSlider.Value:F1}x";
        TailHeadLabel.Text   = $"{TailHeadSlider.Value:F1}x";
    }

    private void StepSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)   { UpdateLabels(); Save(); }
    private void AnimTime_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)   { UpdateLabels(); Save(); }
    private void AccelDelta_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateLabels(); Save(); }
    private void AccelMax_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)   { UpdateLabels(); Save(); }
    private void TailHead_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)   { UpdateLabels(); Save(); }
    private void Toggle_Changed(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        if (_loading) return;
        if (AnimTimeSlider is null) return;  // Belt-and-suspenders during construction
        App.Settings.Update(s =>
        {
            var p = s.GlobalProfile;
            p.StepSize             = (int)StepSizeSlider.Value;
            p.AnimationTime        = (int)AnimTimeSlider.Value;
            p.AccelerationDelta    = (int)AccelDeltaSlider.Value;
            p.AccelerationMax      = AccelMaxSlider.Value;
            p.TailToHeadRatio      = TailHeadSlider.Value;
            p.AnimationEasing      = EasingToggle.IsChecked == true;
            p.ShiftKeyHorizontal   = ShiftHorizToggle.IsChecked == true;
            p.HorizontalSmoothness = HorizSmoothnessToggle.IsChecked == true;
            p.ReverseDirection     = ReverseToggle.IsChecked == true;
            p.ClickToStop          = ClickStopToggle.IsChecked == true;
        });
    }
}
