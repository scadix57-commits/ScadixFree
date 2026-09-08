using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Scadix.Designer;

public partial class FirstScreen : Window
{
    public FirstScreen()
    {
        InitializeComponent();
    }


    #region Window Controls

    private Window GetParentWindow()
    {
        return TopLevel.GetTopLevel(this) as Window;
    }
    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        var window = GetParentWindow();
        if (window != null)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        var window = GetParentWindow();
        if (window != null)
        {
            if (window.WindowState == WindowState.Maximized)
                window.WindowState = WindowState.Normal;
            else
                window.WindowState = WindowState.Maximized;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var window = GetParentWindow();
        window?.Close();
    }

    private void TitleBar_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Only start a window drag for left-button presses
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            var window = GetParentWindow();
            try
            {
                window?.BeginMoveDrag(e);
            }
            catch
            {
                // Some platforms or states may not allow BeginMoveDrag; ignore failures
            }
        }
    }
    #endregion
}