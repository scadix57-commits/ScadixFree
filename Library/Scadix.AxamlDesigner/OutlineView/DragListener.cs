 
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Scadix.AxamlDesigner.OutlineView
{
    // Avalonia note: Unlike WPF, pointer capture is implicit in Avalonia.
    // CaptureMouse() is NOT needed - Avalonia automatically captures the pointer
    // on PointerPressed. Calling Capture(target) manually would steal capture
    // from child controls (e.g. TreeViewItem) and break their click/expand behavior.
    public class DragListener
    {
        private readonly Control target;
        private bool ready;
        private Point startPoint;

        public DragListener(Control target)
        {
            this.target = target;
          
            target.AddHandler(InputElement.PointerPressedEvent, PointerPressed, handledEventsToo: true);
            target.AddHandler(InputElement.PointerMovedEvent, PointerMoved, handledEventsToo: true);
            target.AddHandler(InputElement.PointerReleasedEvent, PointerReleased, handledEventsToo: true);
        }

        
        public event EventHandler<PointerEventArgs> DragStarted;

        private void PointerPressed(object sender, PointerPressedEventArgs e)
        {
            
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                ready = true;
                startPoint = e.GetPosition(target);
               
            }
        }

        private void PointerMoved(object sender, PointerEventArgs e)
        {
            if (ready)
            {
                // Stop tracking if left button was released
                if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                {
                    ready = false;
                    return;
                }
                var currentPoint = e.GetPosition(target);
               
                const double MinDragDistance = 4.0;
                if (Math.Abs(currentPoint.X - startPoint.X) >= MinDragDistance ||
                    Math.Abs(currentPoint.Y - startPoint.Y) >= MinDragDistance)
                {
                    ready = false;
                    DragStarted?.Invoke(this, e);
                }
            }
        }

        private void PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            ready = false;
           
        }
    }
}
