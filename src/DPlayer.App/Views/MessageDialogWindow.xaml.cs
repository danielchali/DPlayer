using System.Windows;
using System.Windows.Controls;

namespace DPlayer.App.Views;

public partial class MessageDialogWindow : Window
{
    private bool _confirmed;

    private MessageDialogWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    public static void ShowMessage(Window? owner, string title, string message)
    {
        var dialog = Create(owner, title, message);
        AddButton(dialog, "OK", isDefault: true, isCancel: true, isAccent: true, () => dialog.Close());
        dialog.ShowDialog();
    }

    public static bool ShowConfirm(Window? owner, string title, string message)
    {
        var dialog = Create(owner, title, message);
        AddButton(dialog, "Yes", isDefault: true, isCancel: false, isAccent: true, () =>
        {
            dialog._confirmed = true;
            dialog.Close();
        });
        AddButton(dialog, "No", isDefault: false, isCancel: true, isAccent: false, () => dialog.Close());
        dialog.ShowDialog();
        return dialog._confirmed;
    }

    private static MessageDialogWindow Create(Window? owner, string title, string message) =>
        new(title, message)
        {
            Owner = owner ?? Application.Current.MainWindow
        };

    private static void AddButton(
        MessageDialogWindow dialog,
        string label,
        bool isDefault,
        bool isCancel,
        bool isAccent,
        Action onClick)
    {
        var button = new Button
        {
            Content = label,
            IsDefault = isDefault,
            IsCancel = isCancel,
            MinWidth = 82,
            Margin = dialog.ButtonPanel.Children.Count > 0 ? new Thickness(8, 0, 0, 0) : new Thickness(0)
        };

        button.Style = (Style)Application.Current.FindResource(
            isAccent ? "AccentButtonStyle" : "TextButtonStyle");

        button.Click += (_, _) => onClick();
        dialog.ButtonPanel.Children.Add(button);
    }
}
