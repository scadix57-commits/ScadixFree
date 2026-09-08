

using Avalonia.Input;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia Cursor type
    /// Converts between string representations and Cursor objects
    /// Supports standard cursor types: Default, Arrow, Hand, IBeam, Cross, etc.
    /// </summary>
    public class CursorConverter : TypeConverter
    {
        #region Conversion Checks

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        #endregion

        #region Conversion Methods

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str) return ParseCursor(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Cursor cursor)
            {
                return CursorToString(cursor);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Parses a string into a Cursor
        /// </summary>
        private static Cursor ParseCursor(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return Cursor.Default;
            str = str.Trim();

            // Standard cursors
            if (str.Equals("Default", StringComparison.OrdinalIgnoreCase) || str.Equals("Arrow", StringComparison.OrdinalIgnoreCase)) return Cursor.Default;
            if (str.Equals("Hand", StringComparison.OrdinalIgnoreCase) || str.Equals("Pointer", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.Hand);
            if (str.Equals("IBeam", StringComparison.OrdinalIgnoreCase) || str.Equals("Text", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.Ibeam);
            if (str.Equals("Cross", StringComparison.OrdinalIgnoreCase) || str.Equals("Crosshair", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.Cross);
            if (str.Equals("Wait", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.Wait);
            if (str.Equals("Help", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.Help);

            // Resize cursors
            if (str.Equals("SizeAll", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.SizeAll);
            if (str.Equals("SizeNESW", StringComparison.OrdinalIgnoreCase) || str.Equals("NeswResize", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.TopLeftCorner);
            if (str.Equals("SizeNS", StringComparison.OrdinalIgnoreCase) || str.Equals("NsResize", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.SizeNorthSouth);
            if (str.Equals("SizeNWSE", StringComparison.OrdinalIgnoreCase) || str.Equals("NwseResize", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.TopRightCorner);
            if (str.Equals("SizeWE", StringComparison.OrdinalIgnoreCase) || str.Equals("EwResize", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.SizeWestEast);

            // Directional resize cursors
            if (str.Equals("LeftSide", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.LeftSide);
            if (str.Equals("RightSide", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.RightSide);
            if (str.Equals("TopSide", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.TopSide);
            if (str.Equals("BottomSide", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.BottomSide);
            if (str.Equals("TopLeftCorner", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.TopLeftCorner);
            if (str.Equals("TopRightCorner", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.TopRightCorner);
            if (str.Equals("BottomLeftCorner", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.BottomLeftCorner);
            if (str.Equals("BottomRightCorner", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.BottomRightCorner);

            // Other cursors
            if (str.Equals("No", StringComparison.OrdinalIgnoreCase) || str.Equals("NotAllowed", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.No);
            if (str.Equals("AppStarting", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.AppStarting);
            if (str.Equals("DragMove", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.DragMove);
            if (str.Equals("DragCopy", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.DragCopy);
            if (str.Equals("DragLink", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.DragLink);
            if (str.Equals("None", StringComparison.OrdinalIgnoreCase)) return new Cursor(StandardCursorType.None);

            throw new FormatException($"Invalid Cursor format: {str}");
        }

        /// <summary>
        /// Converts a Cursor to its string representation
        /// </summary>
        private static string CursorToString(Cursor cursor)
        {
            if (cursor == Cursor.Default) return "Default";
            var cursorType = GetStandardCursorType(cursor);

            return cursorType switch
            {
                StandardCursorType.Arrow => "Arrow",
                StandardCursorType.Hand => "Hand",
                StandardCursorType.Ibeam => "IBeam",
                StandardCursorType.Cross => "Cross",
                StandardCursorType.Wait => "Wait",
                StandardCursorType.Help => "Help",
                StandardCursorType.SizeAll => "SizeAll",
                StandardCursorType.SizeNorthSouth => "SizeNS",
                StandardCursorType.SizeWestEast => "SizeWE",
                StandardCursorType.TopLeftCorner => "TopLeftCorner",
                StandardCursorType.TopRightCorner => "TopRightCorner",
                StandardCursorType.BottomLeftCorner => "BottomLeftCorner",
                StandardCursorType.BottomRightCorner => "BottomRightCorner",
                StandardCursorType.LeftSide => "LeftSide",
                StandardCursorType.RightSide => "RightSide",
                StandardCursorType.TopSide => "TopSide",
                StandardCursorType.BottomSide => "BottomSide",
                StandardCursorType.No => "No",
                StandardCursorType.AppStarting => "AppStarting",
                StandardCursorType.DragMove => "DragMove",
                StandardCursorType.DragCopy => "DragCopy",
                StandardCursorType.DragLink => "DragLink",
                StandardCursorType.None => "None",
                _ => "Default"
            };
        }

        /// <summary>
        /// Gets the StandardCursorType from a Cursor instance
        /// </summary>
        private static StandardCursorType GetStandardCursorType(Cursor cursor)
        {
            if (cursor == Cursor.Default) return StandardCursorType.Arrow;
            foreach (StandardCursorType type in Enum.GetValues(typeof(StandardCursorType)))
            {
                if (cursor.Equals(new Cursor(type))) return type;
            }
            return StandardCursorType.Arrow;
        }

        #endregion
    }
}
