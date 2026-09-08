using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class NotificationsPage : UserControl
{
    public NotificationsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        DisplayBalloonCheckBox.IsChecked = s.DisplayBalloonNotifications;
        PlaySoundCheckBox.IsChecked = s.PlaySoundNotifications;
        ShowInToolWindowCheckBox.IsChecked = s.ShowNotificationsInToolWindow;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        s.DisplayBalloonNotifications = DisplayBalloonCheckBox.IsChecked ?? true;
        s.PlaySoundNotifications = PlaySoundCheckBox.IsChecked ?? false;
        s.ShowNotificationsInToolWindow = ShowInToolWindowCheckBox.IsChecked ?? true;
    }
}
