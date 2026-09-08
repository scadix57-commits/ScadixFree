
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Scadix.AxamlDesign;
using Scadix.AxamlDom.Converters;
using System.ComponentModel;
using System.Windows.Input;
using Path = Avalonia.Controls.Shapes.Path;

namespace Scadix.AxamlDesigner
{
	public static class BasicMetadata
	{
		static bool registered;

		public static void Register()
		{
			if (registered) return;
			registered = true;

			#region Standard Values
			Metadata.AddStandardValues(typeof(Brush), typeof(Brushes));
			Metadata.AddStandardValues(typeof(Color), typeof(Colors));
			Metadata.AddStandardValues(typeof(FontFamily), FontManager.Current.SystemFonts);
			Metadata.AddStandardValues(typeof(FontWeight), typeof(FontWeight));
			Metadata.AddStandardValues(typeof(FontStyle), typeof(FontStyle));
		 
			#endregion

			#region Type Converters
			// Register global type converters for Avalonia types
			TypeDescriptor.AddAttributes(typeof(RelativePoint), new TypeConverterAttribute(typeof(RelativePointConverter)));
			TypeDescriptor.AddAttributes(typeof(FontFamily), new TypeConverterAttribute(typeof(FontFamilyConverter)));
			TypeDescriptor.AddAttributes(typeof(Color), new TypeConverterAttribute(typeof(ColorConverter)));
			TypeDescriptor.AddAttributes(typeof(IBrush), new TypeConverterAttribute(typeof(BrushConverter)));
			TypeDescriptor.AddAttributes(typeof(BoxShadows), new TypeConverterAttribute(typeof(BoxShadowsConverter)));
			TypeDescriptor.AddAttributes(typeof(IImage), new TypeConverterAttribute(typeof(BitmapConverter)));
			TypeDescriptor.AddAttributes(typeof(Thickness), new TypeConverterAttribute(typeof(ThicknessConverter)));
			TypeDescriptor.AddAttributes(typeof(CornerRadius), new TypeConverterAttribute(typeof(CornerRadiusConverter)));
			TypeDescriptor.AddAttributes(typeof(Point), new TypeConverterAttribute(typeof(PointConverter)));
			TypeDescriptor.AddAttributes(typeof(Rect), new TypeConverterAttribute(typeof(RectConverter)));
			TypeDescriptor.AddAttributes(typeof(Size), new TypeConverterAttribute(typeof(SizeConverter)));
			TypeDescriptor.AddAttributes(typeof(Matrix), new TypeConverterAttribute(typeof(MatrixConverter)));
			TypeDescriptor.AddAttributes(typeof(GridLength), new TypeConverterAttribute(typeof(GridLengthConverter)));
			TypeDescriptor.AddAttributes(typeof(ColumnDefinitions), new TypeConverterAttribute(typeof(ColumnDefinitionsConverter)));
			TypeDescriptor.AddAttributes(typeof(RowDefinitions), new TypeConverterAttribute(typeof(RowDefinitionsConverter)));
			TypeDescriptor.AddAttributes(typeof(Avalonia.Controls.Documents.Inline), new TypeConverterAttribute(typeof(InlineConverter)));
			TypeDescriptor.AddAttributes(typeof(Geometry), new TypeConverterAttribute(typeof(GeometryConverter)));
			TypeDescriptor.AddAttributes(typeof(Transform), new TypeConverterAttribute(typeof(TransformConverter)));
			TypeDescriptor.AddAttributes(typeof(FontWeight), new TypeConverterAttribute(typeof(FontWeightConverter)));
			TypeDescriptor.AddAttributes(typeof(FontStyle), new TypeConverterAttribute(typeof(FontStyleConverter)));
			TypeDescriptor.AddAttributes(typeof(FontStretch), new TypeConverterAttribute(typeof(FontStretchConverter)));
			TypeDescriptor.AddAttributes(typeof(Cursor), new TypeConverterAttribute(typeof(CursorConverter)));
            TypeDescriptor.AddAttributes(typeof(WindowStartupLocation), new TypeConverterAttribute(typeof(EnumConverter)));
            TypeDescriptor.AddAttributes(typeof(WindowIcon), new TypeConverterAttribute(typeof(IconConverter)));
			TypeDescriptor.AddAttributes(typeof(IterationCount), new TypeConverterAttribute(typeof(IterationCountConverter)));
			TypeDescriptor.AddAttributes(typeof(Cue), new TypeConverterAttribute(typeof(CueConverter)));
			TypeDescriptor.AddAttributes(typeof(KeySpline), new TypeConverterAttribute(typeof(KeySplineConverter)));
			TypeDescriptor.AddAttributes(typeof(Selector), new TypeConverterAttribute(typeof(SelectorConverter)));
			TypeDescriptor.AddAttributes(typeof(Binding), new TypeConverterAttribute(typeof(BindingConverter)));
			#endregion

			#region Popular Properties
			Metadata.AddPopularProperty(Shape.FillProperty);
			Metadata.AddPopularProperty(Shape.StrokeProperty);
			Metadata.AddPopularProperty(Shape.StrokeThicknessProperty);
			Metadata.AddPopularProperty(Shape.StretchProperty);
			Metadata.AddPopularProperty(Shape.StrokeMiterLimitProperty);

			Metadata.AddPopularProperty(Line.StartPointProperty);
			Metadata.AddPopularProperty(Line.EndPointProperty);
			Metadata.AddPopularProperty(Polygon.PointsProperty);
			Metadata.AddPopularProperty(Polyline.PointsProperty);
			Metadata.AddPopularProperty(Path.DataProperty);

			Metadata.AddPopularProperty(ItemsControl.ItemsSourceProperty);
			Metadata.AddPopularProperty(ItemsControl.ItemTemplateProperty);
			Metadata.AddPopularProperty(typeof(ItemsControl), "Items");

			Metadata.AddPopularProperty(Image.SourceProperty);

			Metadata.AddPopularProperty(TextBlock.TextProperty);
			Metadata.AddPopularProperty(TextBlock.FontSizeProperty);
			Metadata.AddPopularProperty(TextBlock.ForegroundProperty);
			Metadata.AddPopularProperty(TextBlock.FontFamilyProperty);
			Metadata.AddPopularProperty(TextBlock.FontWeightProperty);
			Metadata.AddPopularProperty(TextBlock.TextWrappingProperty);
			Metadata.AddPopularProperty(TextBlock.TextTrimmingProperty);

			Metadata.AddPopularProperty(TextBox.TextProperty);

			Metadata.AddPopularProperty(DockPanel.LastChildFillProperty);
			Metadata.AddPopularProperty(Expander.IsExpandedProperty);
			Metadata.AddPopularProperty(RangeBase.ValueProperty);
			Metadata.AddPopularProperty(RangeBase.MinimumProperty);
			Metadata.AddPopularProperty(RangeBase.MaximumProperty);
			Metadata.AddPopularProperty(RangeBase.LargeChangeProperty);
			Metadata.AddPopularProperty(RangeBase.SmallChangeProperty);

			Metadata.AddPopularProperty(ToggleButton.IsCheckedProperty);

			Metadata.AddPopularProperty(Window.TitleProperty);
			Metadata.AddPopularProperty(Window.SizeToContentProperty);
			Metadata.AddPopularProperty(Window.ShowInTaskbarProperty);
			Metadata.AddPopularProperty(Window.IconProperty);
			Metadata.AddPopularProperty(Window.CanResizeProperty);

			Metadata.AddPopularProperty(Rectangle.RadiusXProperty);
			Metadata.AddPopularProperty(Rectangle.RadiusYProperty);

			Metadata.AddPopularProperty(Layoutable.WidthProperty);
			Metadata.AddPopularProperty(Layoutable.HeightProperty);
			Metadata.AddPopularProperty(Layoutable.MarginProperty);
			Metadata.AddPopularProperty(Layoutable.HorizontalAlignmentProperty);
			Metadata.AddPopularProperty(Layoutable.VerticalAlignmentProperty);

			Metadata.AddPopularProperty(UniformGrid.ColumnsProperty);
			Metadata.AddPopularProperty(ScrollBar.OrientationProperty);
			Metadata.AddPopularProperty(ContentControl.ContentProperty);
			Metadata.AddPopularProperty(ContentControl.VerticalContentAlignmentProperty);

			Metadata.AddPopularProperty(Popup.IsOpenProperty);
			Metadata.AddPopularProperty(Popup.HorizontalOffsetProperty);
			Metadata.AddPopularProperty(Popup.VerticalOffsetProperty);

			Metadata.AddPopularProperty(StyledElement.NameProperty);

			Metadata.AddPopularProperty(Button.IsDefaultProperty);
			Metadata.AddPopularProperty(Button.IsCancelProperty);

			Metadata.AddPopularProperty(Visual.RenderTransformOriginProperty);
			Metadata.AddPopularProperty(Visual.RenderTransformProperty);
			Metadata.AddPopularProperty(Visual.IsVisibleProperty);

			Metadata.AddPopularProperty(Panel.BackgroundProperty);
			Metadata.AddPopularProperty(StackPanel.OrientationProperty);
			Metadata.AddPopularProperty(ListBox.SelectionModeProperty);

			Metadata.AddPopularProperty(Border.BorderBrushProperty);
			Metadata.AddPopularProperty(Border.CornerRadiusProperty);
			Metadata.AddPopularProperty(Border.BorderThicknessProperty);
			Metadata.AddPopularProperty(Border.PaddingProperty);

			Metadata.AddPopularProperty(TreeViewItem.IsSelectedProperty);
			Metadata.AddPopularProperty(HeaderedContentControl.HeaderProperty);
			Metadata.AddPopularProperty(InputElement.IsHitTestVisibleProperty);
			#endregion

			#region Attached Properties
			Metadata.AddPopularProperty(Grid.RowProperty);
			Metadata.AddPopularProperty(Grid.RowSpanProperty);
			Metadata.AddPopularProperty(Grid.ColumnProperty);
			Metadata.AddPopularProperty(Grid.ColumnSpanProperty);
			Metadata.AddPopularProperty(DockPanel.DockProperty);
			Metadata.AddPopularProperty(Canvas.LeftProperty);
			Metadata.AddPopularProperty(Canvas.TopProperty);
			Metadata.AddPopularProperty(Canvas.RightProperty);
			Metadata.AddPopularProperty(Canvas.BottomProperty);
			#endregion

			#region Binding Properties
			Metadata.AddPopularProperty(typeof(Binding), "Path");
			Metadata.AddPopularProperty(typeof(Binding), "Source");
			Metadata.AddPopularProperty(typeof(Binding), "Mode");
			Metadata.AddPopularProperty(typeof(Binding), "RelativeSource");
			Metadata.AddPopularProperty(typeof(Binding), "ElementName");
			Metadata.AddPopularProperty(typeof(Binding), "Converter");
			#endregion

			#region Style and Resource Properties
			Metadata.AddPopularProperty(typeof(Style), "Selector");
			Metadata.AddPopularProperty(typeof(Style), "Setters");
			Metadata.AddPopularProperty(typeof(ResourceDictionary), "Source");
			Metadata.AddPopularProperty(typeof(ResourceDictionary), "MergedDictionaries");
			Metadata.AddPopularProperty(typeof(Styles), "Resources");
			#endregion

			#region ControlTheme Properties
			Metadata.AddPopularProperty(typeof(ControlTheme), "TargetType");
			Metadata.AddPopularProperty(typeof(ControlTheme), "BasedOn");
			Metadata.AddPopularProperty(typeof(ControlTheme), "Children");
			#endregion

			#region Setter Properties
			Metadata.AddPopularProperty(typeof(Setter), "Property");
			Metadata.AddPopularProperty(typeof(Setter), "Value");
			#endregion

			#region Value Ranges
			Metadata.AddValueRange(Canvas.BottomProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(Canvas.LeftProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(Canvas.TopProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(Canvas.RightProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(ColumnDefinition.MaxWidthProperty, 0, double.PositiveInfinity);
			Metadata.AddValueRange(Control.MaxHeightProperty, 0, double.PositiveInfinity);
			Metadata.AddValueRange(Control.MaxWidthProperty, 0, double.PositiveInfinity);
			Metadata.AddValueRange(Grid.ColumnSpanProperty, double.Epsilon, double.MaxValue);
			Metadata.AddValueRange(Grid.RowSpanProperty, double.Epsilon, double.MaxValue);
			Metadata.AddValueRange(GridSplitter.KeyboardIncrementProperty, double.Epsilon, double.MaxValue);
			Metadata.AddValueRange(GridSplitter.DragIncrementProperty, double.Epsilon, double.MaxValue);
			Metadata.AddValueRange(RangeBase.ValueProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(RangeBase.MaximumProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(RangeBase.MinimumProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(RepeatButton.IntervalProperty, double.Epsilon, double.MaxValue);
			Metadata.AddValueRange(RowDefinition.MaxHeightProperty, 0, double.PositiveInfinity);
			Metadata.AddValueRange(Slider.TickFrequencyProperty, double.MinValue, double.MaxValue);
			Metadata.AddValueRange(TextBlock.FontSizeProperty, double.Epsilon, double.MaxValue);
			Metadata.AddValueRange(ScrollBar.ViewportSizeProperty, 0, double.PositiveInfinity);
			Metadata.AddValueRange(Control.OpacityProperty, 0, 1);
			#endregion

			#region Hidden Properties
			Metadata.HideProperty(typeof(Visual), "Bounds");
			Metadata.HideProperty(typeof(Control), "RenderSize");
			Metadata.HideProperty(StyledElement.NameProperty);
			Metadata.HideProperty(typeof(Window), "Owner");
			#endregion

			#region Popular Controls
			Metadata.AddPopularControl(typeof(Button));
			Metadata.AddPopularControl(typeof(Border));
			Metadata.AddPopularControl(typeof(Canvas));
			Metadata.AddPopularControl(typeof(CheckBox));
			Metadata.AddPopularControl(typeof(ComboBox));
			Metadata.AddPopularControl(typeof(DataGrid));
			Metadata.AddPopularControl(typeof(DockPanel));
			Metadata.AddPopularControl(typeof(Expander));
			Metadata.AddPopularControl(typeof(Grid));
			Metadata.AddPopularControl(typeof(Image));
			Metadata.AddPopularControl(typeof(Label));
			Metadata.AddPopularControl(typeof(ListBox));
			Metadata.AddPopularControl(typeof(Menu));
			Metadata.AddPopularControl(typeof(ProgressBar));
			Metadata.AddPopularControl(typeof(RadioButton));
			Metadata.AddPopularControl(typeof(StackPanel));
			Metadata.AddPopularControl(typeof(ScrollViewer));
			Metadata.AddPopularControl(typeof(Slider));
			Metadata.AddPopularControl(typeof(TabControl));
			Metadata.AddPopularControl(typeof(TextBlock));
			Metadata.AddPopularControl(typeof(TextBox));
			Metadata.AddPopularControl(typeof(TreeView));
			Metadata.AddPopularControl(typeof(Viewbox));
			Metadata.AddPopularControl(typeof(WrapPanel));
			Metadata.AddPopularControl(typeof(Line));
			Metadata.AddPopularControl(typeof(Polyline));
			Metadata.AddPopularControl(typeof(Ellipse));
			Metadata.AddPopularControl(typeof(Rectangle));
			Metadata.AddPopularControl(typeof(Path));
			Metadata.AddPopularControl(typeof(ControlTheme));
			Metadata.AddPopularControl(typeof(Style));
			Metadata.AddPopularControl(typeof(Setter));
			#endregion

			#region Default Sizes
			Metadata.AddDefaultSize(typeof(TextBlock), new Size(double.NaN, double.NaN));
			Metadata.AddDefaultSize(typeof(CheckBox), new Size(double.NaN, double.NaN));
			Metadata.AddDefaultSize(typeof(Image), new Size(double.NaN, double.NaN));

			Metadata.AddDefaultSize(typeof(Control), new Size(120, 100));
			Metadata.AddDefaultSize(typeof(ContentControl), new Size(120, 20));
			Metadata.AddDefaultSize(typeof(Button), new Size(75, 23));
			Metadata.AddDefaultSize(typeof(ToggleButton), new Size(75, 23));

			Metadata.AddDefaultSize(typeof(Slider), new Size(120, 20));
			Metadata.AddDefaultSize(typeof(TextBox), new Size(120, 20));
			Metadata.AddDefaultSize(typeof(ComboBox), new Size(120, 20));
			Metadata.AddDefaultSize(typeof(ProgressBar), new Size(120, 20));

			Metadata.AddDefaultSize(typeof(Menu), new Size(120, 20));
			Metadata.AddDefaultSize(typeof(TreeView), new Size(120, 120));
			Metadata.AddDefaultSize(typeof(Label), new Size(130, 120));
			Metadata.AddDefaultSize(typeof(Expander), new Size(130, 120));
			#endregion

			#region Default Property Values
			Metadata.AddDefaultPropertyValue(typeof(Line), Line.StartPointProperty, new Point(0, 0));
			Metadata.AddDefaultPropertyValue(typeof(Line), Line.EndPointProperty, new Point(20, 20));
			Metadata.AddDefaultPropertyValue(typeof(Line), Line.StrokeProperty, Brushes.Black);
			Metadata.AddDefaultPropertyValue(typeof(Line), Line.StrokeThicknessProperty, 2d);
			Metadata.AddDefaultPropertyValue(typeof(Line), Line.StretchProperty, Stretch.None);

			Metadata.AddDefaultPropertyValue(typeof(Polyline), Polyline.PointsProperty, new Points { new Point(0, 0), new Point(20, 0), new Point(20, 20) });
			Metadata.AddDefaultPropertyValue(typeof(Polyline), Polyline.StrokeProperty, Brushes.Black);
			Metadata.AddDefaultPropertyValue(typeof(Polyline), Polyline.StrokeThicknessProperty, 2d);
			Metadata.AddDefaultPropertyValue(typeof(Polyline), Polyline.StretchProperty, Stretch.None);

			Metadata.AddDefaultPropertyValue(typeof(Polygon), Polygon.PointsProperty, new Points { new Point(0, 20), new Point(20, 20), new Point(10, 0) });
			Metadata.AddDefaultPropertyValue(typeof(Polygon), Polygon.StrokeProperty, Brushes.Black);
			Metadata.AddDefaultPropertyValue(typeof(Polygon), Polygon.StrokeThicknessProperty, 2d);
			Metadata.AddDefaultPropertyValue(typeof(Polygon), Polygon.StretchProperty, Stretch.None);

			Metadata.AddDefaultPropertyValue(typeof(Path), Path.StrokeProperty, Brushes.Black);
			Metadata.AddDefaultPropertyValue(typeof(Path), Path.StrokeThicknessProperty, 2d);
			Metadata.AddDefaultPropertyValue(typeof(Path), Path.StretchProperty, Stretch.None);

			Metadata.AddDefaultPropertyValue(typeof(Rectangle), Rectangle.FillProperty, Brushes.Transparent);
			Metadata.AddDefaultPropertyValue(typeof(Rectangle), Rectangle.StrokeProperty, Brushes.Black);
			Metadata.AddDefaultPropertyValue(typeof(Rectangle), Rectangle.StrokeThicknessProperty, 2d);

			Metadata.AddDefaultPropertyValue(typeof(Ellipse), Ellipse.FillProperty, Brushes.Transparent);
			Metadata.AddDefaultPropertyValue(typeof(Ellipse), Ellipse.StrokeProperty, Brushes.Black);
			Metadata.AddDefaultPropertyValue(typeof(Ellipse), Ellipse.StrokeThicknessProperty, 2d);
			#endregion
		}
	}
}
