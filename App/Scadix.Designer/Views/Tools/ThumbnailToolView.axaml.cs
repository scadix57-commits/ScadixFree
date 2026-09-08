using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Scadix.Designer;

public partial class ThumbnailToolView : UserControl
{
    public ThumbnailToolView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
            DataContext = this.FindAncestorOfType<Window>()?.DataContext;
    }
}
