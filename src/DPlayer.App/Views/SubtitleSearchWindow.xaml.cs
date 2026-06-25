using System.Windows;

namespace DPlayer.App.Views;

public partial class SubtitleSearchWindow : Window
{
    public SubtitleSearchWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
