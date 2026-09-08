using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;

namespace Scadix.AxamlDesigner
{
    public static class Keyboard {
        public static bool IsKeyDown(Key key) => false;
        public static KeyModifiers Modifiers => KeyModifiers.None;
    }

    public static class DummyExtensions {
        public static StreamGeometry GetFlattenedPathGeometry(this StreamGeometry g) => (StreamGeometry)g.Clone();
        public static Matrix Invert(this Matrix m) { return m.Invert(); } // Doesn't fix Inverse property because it's ITransform.Inverse
    }

    public class GeometryConverter {
        public string ConvertToInvariantString(Geometry g) => g.ToString();
    }

}
