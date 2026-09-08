using Avalonia.Input;
using Avalonia.Interactivity;
using System.Windows.Input;

namespace Scadix.AxamlDesign.UIExtensions
{
    public class MouseHorizontalWheelEventArgs : RoutedEventArgs
    {
        public int HorizontalDelta { get; }
        public IPointer Pointer { get; }
        public int Timestamp { get; }

        public MouseHorizontalWheelEventArgs(IPointer pointer, int timestamp, int horizontalDelta)
        {
            Pointer = pointer;
            Timestamp = timestamp;
            HorizontalDelta = horizontalDelta;
        }
    }
}
