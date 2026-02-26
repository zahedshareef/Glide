using SmoothScroller.UI.Pages;
using System.Windows;

namespace SmoothScroller;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Navigate to the default page using the Frame directly
        ContentFrame.Navigate(new GeneralPage());

        // Route NavigationViewItem clicks to the Frame
        NavGeneral.Click  += (_, _) => ContentFrame.Navigate(new GeneralPage());
        NavScroll.Click   += (_, _) => ContentFrame.Navigate(new ScrollPage());
        NavPerApp.Click   += (_, _) => ContentFrame.Navigate(new PerAppPage());
        NavAdvanced.Click += (_, _) => ContentFrame.Navigate(new AdvancedPage());
        NavDiag.Click     += (_, _) => ContentFrame.Navigate(new DiagnosticsPage());
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}