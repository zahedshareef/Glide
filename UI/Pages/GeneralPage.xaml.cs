using System.IO;
using System.Windows;
using System.Windows.Controls;
using SmoothScroller.AutoStart;

namespace SmoothScroller.UI.Pages;

public partial class GeneralPage : System.Windows.Controls.Page
{
    private bool _loading;

    public GeneralPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        var s = App.Settings.Current;
        EnableToggle.IsChecked         = s.IsEnabled;
        AutoStartToggle.IsChecked      = s.AutoStart;
        StartMinimizedToggle.IsChecked = s.StartMinimized;

        foreach (ComboBoxItem item in BypassKeyCombo.Items)
        {
            if (int.TryParse(item.Tag?.ToString(), out int tag) && tag == s.BypassHotkeyVk)
            {
                BypassKeyCombo.SelectedItem = item;
                break;
            }
        }
        if (BypassKeyCombo.SelectedIndex < 0) BypassKeyCombo.SelectedIndex = 1;
        _loading = false;
    }

    private void EnableToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Update(s => s.IsEnabled = EnableToggle.IsChecked == true);
        App.Tray.UpdateIcon(App.Settings.Current.IsEnabled);
    }

    private void AutoStartToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool enabled = AutoStartToggle.IsChecked == true;
        App.Settings.Update(s => s.AutoStart = enabled);
        StartupManager.Sync(enabled);
    }

    private void StartMinimized_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Update(s => s.StartMinimized = StartMinimizedToggle.IsChecked == true);
    }

    private void BypassKeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (BypassKeyCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int vk))
        {
            App.Settings.Update(s => s.BypassHotkeyVk = vk);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = "smoothscroller-settings.json" };
        if (dlg.ShowDialog() == true)
            File.WriteAllText(dlg.FileName, App.Settings.ExportJson());
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                App.Settings.ImportJson(File.ReadAllText(dlg.FileName));
                OnLoaded(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Import failed: {ex.Message}", "SmoothScroller",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
