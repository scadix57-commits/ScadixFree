using Avalonia;
using System;
using System.Collections.Generic;
using System.Text;

namespace Scadix.AxamlDesigner.Extensions
{
    /// <summary>
    /// Extension methods for Thickness to provide With* methods for Avalonia 11.x compatibility
    /// </summary>
    public static class ThicknessExtensions
    {
        public static Thickness WithLeft(this Thickness thickness, double left)
        {
            return new Thickness(left, thickness.Top, thickness.Right, thickness.Bottom);
        }

        public static Thickness WithTop(this Thickness thickness, double top)
        {
            return new Thickness(thickness.Left, top, thickness.Right, thickness.Bottom);
        }

        public static Thickness WithRight(this Thickness thickness, double right)
        {
            return new Thickness(thickness.Left, thickness.Top, right, thickness.Bottom);
        }

        public static Thickness WithBottom(this Thickness thickness, double bottom)
        {
            return new Thickness(thickness.Left, thickness.Top, thickness.Right, bottom);
        }
    }
}
