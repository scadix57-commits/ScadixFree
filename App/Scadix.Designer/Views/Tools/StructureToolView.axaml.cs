using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Scadix.Designer;

public partial class OutlineToolView : UserControl
{
    public OutlineToolView()
    {
        InitializeComponent();
        // Bind DataContext to Window.DataContext (Shell) — same pattern as AvaloniaStudio.
        // Works even after Dock re-creates this wrapper on tab drag/move.
        AttachedToVisualTree += (_, _) =>
            DataContext = this.FindAncestorOfType<Window>()?.DataContext;
    }
}
