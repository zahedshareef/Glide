using SmoothScroller.Settings.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

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

        int? step = int.TryParse(OverrideStepBox.Text.Trim(), out var s) ? s : null;
        int? anim = int.TryParse(OverrideAnimBox.Text.Trim(), out var a) ? a : null;

        App.Settings.Update(s =>
        {
            if (!s.AppOverrides.TryGetValue(name, out var profile))
            {
                profile = new ScrollProfile();
                s.AppOverrides[name] = profile;
            }
            if (step.HasValue) profile.StepSize = step;
            if (anim.HasValue) profile.AnimationTime = anim;
        });

        // Update list
        var existing = _overrides.FirstOrDefault(x => x.AppName == name);
        if (existing != null)
        {
            existing.StepSize = step?.ToString() ?? existing.StepSize;
            existing.AnimTime = anim?.ToString() ?? existing.AnimTime;
            // Force refresh visually if needed, but simplified here by replacing:
            _overrides.Remove(existing);
            _overrides.Add(existing);
        }
        else
        {
            _overrides.Add(new AppListEntry
            {
                AppName = name,
                StepSize = step?.ToString() ?? "—",
                AnimTime = anim?.ToString() ?? "—"
            });
        }
        
        OverrideAppBox.Text = "";
        OverrideStepBox.Text = "";
        OverrideAnimBox.Text = "";
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
        }
    }
}
