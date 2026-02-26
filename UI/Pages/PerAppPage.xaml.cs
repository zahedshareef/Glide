using SmoothScroller.Settings.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using Slider = System.Windows.Controls.Slider;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SmoothScroller.UI.Pages;

public class AppListEntry
{
    public string AppName   { get; set; } = "";
    public string EntryType { get; set; } = "Filter";
    public string StepSize  { get; set; } = "—";
    public string AnimTime  { get; set; } = "—";
}

public partial class PerAppPage : System.Windows.Controls.Page
{
    private readonly ObservableCollection<AppListEntry> _overrides = new();
    private readonly ObservableCollection<AppListEntry> _filters = new();
    private bool _loading;

    public PerAppPage()
    {
        InitializeComponent();
        OverridesListView.ItemsSource = _overrides;
        FilterListView.ItemsSource = _filters;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        var s = App.Settings.Current;
        BlacklistRadio.IsChecked = s.FilterMode == FilterMode.Blacklist;
        WhitelistRadio.IsChecked = s.FilterMode == FilterMode.Whitelist;

        _filters.Clear();
        foreach (var app in s.FilterList)
            _filters.Add(new AppListEntry { AppName = app });

        _overrides.Clear();
        foreach (var kv in s.AppOverrides)
            _overrides.Add(new AppListEntry
            {
                AppName   = kv.Key,
                StepSize  = kv.Value.StepSize?.ToString()      ?? "—",
                AnimTime  = kv.Value.AnimationTime?.ToString() ?? "—",
            });
        _loading = false;
    }

    private void FilterMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Update(s =>
            s.FilterMode = BlacklistRadio.IsChecked == true ? FilterMode.Blacklist : FilterMode.Whitelist);
    }

    private void AddFilter_Click(object sender, RoutedEventArgs e)
    {
        var name = FilterAppBox.Text.Trim().ToLowerInvariant().Replace(".exe", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        App.Settings.Update(s =>
        {
            if (!s.FilterList.Contains(name, StringComparer.OrdinalIgnoreCase))
                s.FilterList.Add(name);
        });
        if (!_filters.Any(x => x.AppName == name))
            _filters.Add(new AppListEntry { AppName = name });
        FilterAppBox.Text = "";
    }

    private void AddOverride_Click(object sender, RoutedEventArgs e)
    {
        var name = OverrideAppBox.Text.Trim().ToLowerInvariant().Replace(".exe", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        App.Settings.Update(s =>
        {
            if (!s.AppOverrides.ContainsKey(name))
            {
                s.AppOverrides[name] = new ScrollProfile();
            }
        });

        if (!_overrides.Any(x => x.AppName == name))
        {
            _overrides.Add(new AppListEntry { AppName = name });
        }
        
        OverrideAppBox.Text = "";
    }

    private void RemoveFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string name)
        {
            App.Settings.Update(s =>
            {
                s.FilterList.RemoveAll(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            });
            var entry = _filters.FirstOrDefault(x => x.AppName == name);
            if (entry != null) _filters.Remove(entry);
        }
    }

    private void RemoveOverride_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string name)
        {
            App.Settings.Update(s =>
            {
                s.AppOverrides.Remove(name);
            });
            var entry = _overrides.FirstOrDefault(x => x.AppName == name);
            if (entry != null) _overrides.Remove(entry);

            if (OverridesListView.SelectedItem is AppListEntry selected && selected.AppName == name)
            {
                OverrideDetailPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private string _currentDetailApp = "";
    private bool _updatingDetails = false;

    private void OverridesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OverridesListView.SelectedItem is AppListEntry entry)
        {
            _currentDetailApp = entry.AppName;
            OverrideDetailHeader.Text = $"Settings for {entry.AppName}";
            OverrideDetailPanel.Visibility = Visibility.Visible;
            PopulateDetailPanel();
        }
        else
        {
            OverrideDetailPanel.Visibility = Visibility.Collapsed;
            _currentDetailApp = "";
        }
    }

    private void PopulateDetailPanel()
    {
        if (string.IsNullOrEmpty(_currentDetailApp)) return;
        if (!App.Settings.Current.AppOverrides.TryGetValue(_currentDetailApp, out var profile)) return;

        _updatingDetails = true;

        var g = App.Settings.Current.GlobalProfile;

        void LoadInt(CheckBox chk, Slider sld, TextBlock lbl, int? val, int globalVal, string suffix)
        {
            chk.IsChecked = val == null;
            sld.IsEnabled = val != null;
            sld.Value = val ?? globalVal;
            lbl.Text = $"{(val ?? globalVal)} {suffix}";
        }

        void LoadDbl(CheckBox chk, Slider sld, TextBlock lbl, double? val, double globalVal, string suffix)
        {
            chk.IsChecked = val == null;
            sld.IsEnabled = val != null;
            sld.Value = val ?? globalVal;
            lbl.Text = $"{(val ?? globalVal):0.0}{suffix}";
        }

        void LoadBool(CheckBox chk, Wpf.Ui.Controls.ToggleSwitch tgl, bool? val, bool globalVal)
        {
            chk.IsChecked = val == null;
            tgl.IsEnabled = val != null;
            tgl.IsChecked = val ?? globalVal;
        }

        LoadInt(ChkInheritStep, SldStep, LblStep, profile.StepSize, g.StepSize ?? 120, "px");
        LoadInt(ChkInheritAnim, SldAnim, LblAnim, profile.AnimationTime, g.AnimationTime ?? 360, "ms");
        LoadInt(ChkInheritAccelDelta, SldAccelDelta, LblAccelDelta, profile.AccelerationDelta, g.AccelerationDelta ?? 70, "ms");
        LoadDbl(ChkInheritAccelMax, SldAccelMax, LblAccelMax, profile.AccelerationMax, g.AccelerationMax ?? 7.0, "x");
        LoadDbl(ChkInheritTail, SldTail, LblTail, profile.TailToHeadRatio, g.TailToHeadRatio ?? 3.0, "x");

        LoadBool(ChkInheritEasing, TglEasing, profile.AnimationEasing, g.AnimationEasing ?? true);
        LoadBool(ChkInheritShift, TglShift, profile.ShiftKeyHorizontal, g.ShiftKeyHorizontal ?? true);
        LoadBool(ChkInheritHoriz, TglHoriz, profile.HorizontalSmoothness, g.HorizontalSmoothness ?? true);
        LoadBool(ChkInheritReverse, TglReverse, profile.ReverseDirection, g.ReverseDirection ?? false);
        LoadBool(ChkInheritClickStop, TglClickStop, profile.ClickToStop, g.ClickToStop ?? true);

        _updatingDetails = false;
    }

    private void Detail_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingDetails || string.IsNullOrEmpty(_currentDetailApp)) return;

        App.Settings.Update(s =>
        {
            if (!s.AppOverrides.TryGetValue(_currentDetailApp, out var profile)) return;

            int? GetInt(CheckBox chk, Slider sld) => chk.IsChecked == true ? null : (int)sld.Value;
            double? GetDbl(CheckBox chk, Slider sld) => chk.IsChecked == true ? null : sld.Value;
            bool? GetBool(CheckBox chk, Wpf.Ui.Controls.ToggleSwitch tgl) => chk.IsChecked == true ? null : tgl.IsChecked == true;

            profile.StepSize = GetInt(ChkInheritStep, SldStep);
            profile.AnimationTime = GetInt(ChkInheritAnim, SldAnim);
            profile.AccelerationDelta = GetInt(ChkInheritAccelDelta, SldAccelDelta);
            profile.AccelerationMax = GetDbl(ChkInheritAccelMax, SldAccelMax);
            profile.TailToHeadRatio = GetDbl(ChkInheritTail, SldTail);

            profile.AnimationEasing = GetBool(ChkInheritEasing, TglEasing);
            profile.ShiftKeyHorizontal = GetBool(ChkInheritShift, TglShift);
            profile.HorizontalSmoothness = GetBool(ChkInheritHoriz, TglHoriz);
            profile.ReverseDirection = GetBool(ChkInheritReverse, TglReverse);
            profile.ClickToStop = GetBool(ChkInheritClickStop, TglClickStop);
        });

        // Re-read to format labels accurately while sliding
        PopulateDetailPanel();
    }
}
