using Avalonia;
using Avalonia.Controls;



namespace Scadix.AxamlDom
{
    /// <summary>
    /// Helper Class for the Design Time Properties used by VS and Blend
    /// </summary>
    public class DesignTimeProperties
    {
        static DesignTimeProperties()
        {
            IsHiddenProperty.Changed.AddClassHandler<Visual>(OnIsHiddenPropertyChanged);
        }


        #region IsHidden

        /// <summary>
        /// Getter for <see cref="IsHiddenProperty"/>
        /// </summary>
        public static bool GetIsHidden(AvaloniaObject obj)
        {
            return (bool)obj.GetValue(IsHiddenProperty);
        }

        /// <summary>
        /// Setter for <see cref="IsHiddenProperty"/>
        /// </summary>
        public static void SetIsHidden(AvaloniaObject obj, bool value)
        {
            obj.SetValue(IsHiddenProperty, value);
        }

        /// <summary>
        /// Design-time IsHidden property
        /// </summary>
        public static readonly AvaloniaProperty IsHiddenProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, bool?>("IsHidden", defaultValue: false);


        static void OnIsHiddenPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            // Find the IsVisible property
            var isVisibleProperty = Control.IsVisibleProperty;

            // Unsubscribe previous event handler if any
            if (e.OldValue is bool oldValue && oldValue)
            {
                d.PropertyChanged -= OnVisibilityPropertyChanged;
                d.CoerceValue(isVisibleProperty);
            }

            // Subscribe to PropertyChanged if IsHidden is set to true
            if (e.NewValue is bool newValue && newValue)
            {
                EnsureHidden(d);
                d.PropertyChanged += OnVisibilityPropertyChanged;
            }
        }

        static void OnVisibilityPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Control.IsVisibleProperty)
            {
                var d = sender as AvaloniaObject;
                if (d != null && GetIsHidden(d))
                {
                    EnsureHidden(d);
                }
            }
        }

        static void EnsureHidden(AvaloniaObject d)
        {
            if ((d.GetValue(Control.IsVisibleProperty)))
            {

                d.SetCurrentValue(Control.IsVisibleProperty, false);
            }
            else if (!d.GetValue(Control.IsVisibleProperty))
            {
                d.SetCurrentValue(Control.IsVisibleProperty, true);
            }
        }

        #endregion

        #region IsLocked

        /// <summary>
        /// Getter for <see cref="IsLockedProperty"/>
        /// </summary>
        public static bool GetIsLocked(AvaloniaObject obj)
        {
            return (bool)obj.GetValue(IsLockedProperty);
        }

        /// <summary>
        /// Setter for <see cref="IsLockedProperty"/>
        /// </summary>
        public static void SetIsLocked(AvaloniaObject obj, bool value)
        {
            obj.SetValue(IsLockedProperty, value);
        }

        /// <summary>
        /// Design-time IsLocked property.
        /// </summary>
        public static readonly AvaloniaProperty IsLockedProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, bool>("IsLocked", defaultValue: false);

        #endregion

        #region DataContext
        /// <summary>
        /// Getter for <see cref="DataContextProperty"/>
        /// </summary>
        public static object GetDataContext(AvaloniaObject obj)
        {
            return (object)obj.GetValue(DataContextProperty);
        }

        /// <summary>
        /// Setter for <see cref="DataContextProperty"/>
        /// </summary>
        public static void SetDataContext(AvaloniaObject obj, bool value)
        {
            obj.SetValue(DataContextProperty, value);
        }

        /// <summary>
        /// Design-time data context
        /// </summary>
        public static readonly AvaloniaProperty DataContextProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, object?>("DataContext", false);

        #endregion

        #region DesignSource
        /// <summary>
        /// Getter for <see cref="DesignSourceProperty"/>
        /// </summary>
        public static object GetDesignSource(AvaloniaObject obj)
        {
            return (object)obj.GetValue(DesignSourceProperty);
        }

        /// <summary>
        /// Setter for <see cref="DesignSourceProperty"/>
        /// </summary>
        public static void SetDesignSource(AvaloniaObject obj, bool value)
        {
            obj.SetValue(DesignSourceProperty, value);
        }

        /// <summary>
        /// Design-time design source
        /// </summary>
        public static readonly AvaloniaProperty DesignSourceProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, object?>("DesignSource", false);

        #endregion

        #region DesignWidth
        /// <summary>
        /// Getter for <see cref="DesignWidthProperty"/>
        /// </summary>
        public static double GetDesignWidth(AvaloniaObject obj)
        {
            return (double)obj.GetValue(DesignWidthProperty);
        }

        /// <summary>
        /// Setter for <see cref="DesignWidthProperty"/>
        /// </summary>
        public static void SetDesignWidth(AvaloniaObject obj, double value)
        {
            obj.SetValue(DesignWidthProperty, value);
        }

        /// <summary>
        /// Design-time width
        /// </summary>
        public static readonly AvaloniaProperty DesignWidthProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, double?>("DesignWidth");
        #endregion

        #region DesignHeight
        /// <summary>
        /// Getter for <see cref="DesignHeightProperty"/>
        /// </summary>
        public static double GetDesignHeight(AvaloniaObject obj)
        {
            return (double)obj.GetValue(DesignHeightProperty);
        }

        /// <summary>
        /// Setter for <see cref="DesignHeightProperty"/>
        /// </summary>
        public static void SetDesignHeight(AvaloniaObject obj, double value)
        {
            obj.SetValue(DesignHeightProperty, value);
        }

        /// <summary>
        /// Design-time height
        /// </summary>
        public static readonly AvaloniaProperty DesignHeightProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, double?>("DesignHeight");
        #endregion

        #region LayoutOverrides
        /// <summary>
        /// Getter for <see cref="LayoutOverridesProperty"/>
        /// </summary>
        public static string GetLayoutOverrides(AvaloniaObject obj)
        {
            return (string)obj.GetValue(LayoutOverridesProperty);
        }

        /// <summary>
        /// Setter for <see cref="LayoutOverridesProperty"/>
        /// </summary>
        public static void SetLayoutOverrides(AvaloniaObject obj, string value)
        {
            obj.SetValue(LayoutOverridesProperty, value);
        }

        /// <summary>
        /// Layout-Overrides
        /// </summary>
        public static readonly AvaloniaProperty LayoutOverridesProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, string?>("LayoutOverrides");
        #endregion

        #region LayoutRounding
        /// <summary>
        /// Getter for <see cref="LayoutRoundingProperty"/>
        /// </summary>
        public static bool GetLayoutRounding(AvaloniaObject obj)
        {
            return (bool)obj.GetValue(LayoutRoundingProperty);
        }

        /// <summary>
        /// Setter for <see cref="LayoutRoundingProperty"/>
        /// </summary>
        public static void SetLayoutRounding(AvaloniaObject obj, bool value)
        {
            obj.SetValue(LayoutRoundingProperty, value);
        }

        /// <summary>
        /// Design-time layout rounding
        /// </summary>
        public static readonly AvaloniaProperty LayoutRoundingProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, Visual, bool?>("LayoutRounding");
        #endregion

        #region PreviewWith

        /// <summary>
        ///     Getter for <see cref="PreviewWithProperty" />
        /// </summary>
        public static object GetPreviewWith(AvaloniaObject obj)
        {
            return obj.GetValue(PreviewWithProperty);
        }

        /// <summary>
        ///     Setter for <see cref="PreviewWithProperty" />
        /// </summary>
        public static void SetPreviewWith(AvaloniaObject obj, object value)
        {
            obj.SetValue(PreviewWithProperty, value);
        }

        /// <summary>
        ///     Design-time preview content
        /// </summary>
        public static readonly AttachedProperty<object> PreviewWithProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, AvaloniaObject, object>("PreviewWith");

        #endregion

        #region IsHitTestVisible

        /// <summary>
        ///     Getter for <see cref="IsHitTestVisibleProperty" />
        /// </summary>
        public static bool GetIsHitTestVisible(AvaloniaObject obj)
        {
            return obj.GetValue(IsHitTestVisibleProperty);
        }

        /// <summary>
        ///     Setter for <see cref="IsHitTestVisibleProperty" />
        /// </summary>
        public static void SetIsHitTestVisible(AvaloniaObject obj, bool value)
        {
            obj.SetValue(IsHitTestVisibleProperty, value);
        }

        /// <summary>
        ///     Design-time IsHitTestVisible property
        /// </summary>
        public static readonly AttachedProperty<bool> IsHitTestVisibleProperty =
            AvaloniaProperty.RegisterAttached<DesignTimeProperties, AvaloniaObject, bool>("IsHitTestVisible", defaultValue: true);

        #endregion
    }
}
