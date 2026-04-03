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
    private readonly ObservableCollection<AppListEntry> _entries = new();
    private bool _loading;

    public PerAppPage()
    {
        InitializeComponent();
        AppListView.ItemsSource = _entries;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        var s = App.Settings.Current;
        BlacklistRadio.IsChecked = s.FilterMode == FilterMode.Blacklist;
        WhitelistRadio.IsChecked = s.FilterMode == FilterMode.Whitelist;

        _entries.Clear();
        foreach (var app in s.FilterList)
            _entries.Add(new AppListEntry { AppName = app, EntryType = "Filter" });

        foreach (var kv in s.AppOverrides)
            _entries.Add(new AppListEntry
            {
                AppName   = kv.Key,
                EntryType = "Override",
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

    private void AddToList_Click(object sender, RoutedEventArgs e)
    {
        var name = AppNameBox.Text.Trim().ToLowerInvariant().Replace(".exe", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        App.Settings.Update(s =>
        {
            if (!s.FilterList.Contains(name, StringComparer.OrdinalIgnoreCase))
                s.FilterList.Add(name);
        });
        if (!_entries.Any(x => x.AppName == name))
            _entries.Add(new AppListEntry { AppName = name, EntryType = "Filter" });
        AppNameBox.Text = "";
    }

    private void AddOverride_Click(object sender, RoutedEventArgs e)
    {
        var name = AppNameBox.Text.Trim().ToLowerInvariant().Replace(".exe", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        App.Settings.Update(s =>
        {
            if (!s.AppOverrides.ContainsKey(name))
                s.AppOverrides[name] = new ScrollProfile();
        });
        if (!_entries.Any(x => x.AppName == name && x.EntryType == "Override"))
            _entries.Add(new AppListEntry { AppName = name, EntryType = "Override" });
        AppNameBox.Text = "";
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string name)
        {
            App.Settings.Update(s =>
            {
                s.FilterList.RemoveAll(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                s.AppOverrides.Remove(name);
            });
            var entry = _entries.FirstOrDefault(x => x.AppName == name);
            if (entry != null) _entries.Remove(entry);
        }
    }

    private void AppListView_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
}
