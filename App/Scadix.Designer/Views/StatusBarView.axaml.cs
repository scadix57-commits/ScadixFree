using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Scadix.Designer;

public partial class StatusBarView : UserControl
{
    public StatusBarView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
            DataContext = this.FindAncestorOfType<Window>()?.DataContext;
    }
}
