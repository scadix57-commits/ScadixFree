 

using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesign.UIExtensions;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Extends In-Place editor to edit any text in the designer which is wrapped in the Visual tree under TexBlock
	/// </summary>
	[ExtensionFor(typeof(TextBlock))]
	public class InPlaceEditorExtension : PrimarySelectionAdornerProvider
	{
		AdornerPanel adornerPanel;
		RelativePlacement placement;
		InPlaceEditor editor;
		/// <summary> Is the element in the Visual tree of the extended element which is being edited. </summary>
		TextBlock textBlock;
		Control element;
		DesignPanel designPanel;

		bool isGettingDragged;   // Flag to get/set whether the extended element is dragged.
		bool isMouseDown;        // Flag to get/set whether left-button is down on the element.
		int numClicks;           // No of left-button clicks on the element.

		public InPlaceEditorExtension()
		{
			adornerPanel = new AdornerPanel();
			isGettingDragged = false;
			isMouseDown = false;
			numClicks = 0;
		}

		protected override void OnInitialized()
		{
			base.OnInitialized();
			element = ExtendedItem.Component as Control;
			editor = new InPlaceEditor(ExtendedItem);
			editor.DataContext = element;
			editor.IsVisible = false; // Hide the editor first, It's visibility is governed by mouse events.

			placement = new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Top);
			adornerPanel.Children.Add(editor);
			Adorners.Add(adornerPanel);

			designPanel = ExtendedItem.Services.GetService<IDesignPanel>() as DesignPanel;
			Debug.Assert(designPanel != null);

			/* Add mouse event handlers */
			designPanel.PointerPressed += MouseDown;
			designPanel.PointerReleased += MouseUp;
			(designPanel as Avalonia.Controls.Control)?.AddHandler(Avalonia.Input.InputElement.PointerMovedEvent, (EventHandler<PointerEventArgs>)MouseMove, handledEventsToo: true);

			/* To update the position of Editor in case of resize operation */
			ExtendedItem.PropertyChanged += PropertyChanged;

			eventsAdded = true;
		}

		/// <summary>
		/// Checks whether heigth/width have changed and updates the position of editor
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (textBlock != null)
			{
				if (e.PropertyName == "Width")
				{
					// Use last known pointer position - Mouse.GetPosition not available in Avalonia
					editor.MaxWidth = Math.Max(ModelTools.GetWidth(element), 0);
				}
				if (e.PropertyName == "Height")
				{
					editor.MaxHeight = Math.Max(ModelTools.GetHeight(element), 0);
				}
				AdornerPanel.SetPlacement(editor, placement);
			}
		}

		/// <summary>
		/// Places the handle from a calculated offset using Mouse Positon
		/// </summary>
		/// <param name="text"></param>
		/// <param name="e"></param>
		void PlaceEditor(Visual text, PointerEventArgs e)
		{
			textBlock = text as TextBlock;
			Debug.Assert(textBlock != null);

			/* Gets the offset between the top-left corners of the element and the editor*/
			placement.XOffset = e.GetPosition(element).X - e.GetPosition(textBlock).X - 2.8;
			placement.YOffset = e.GetPosition(element).Y - e.GetPosition(textBlock).Y - 1;
			placement.XRelativeToAdornerWidth = 0;
			placement.XRelativeToContentWidth = 0;
			placement.YRelativeToAdornerHeight = 0;
			placement.YRelativeToContentHeight = 0;

			/* Change data context of the editor to the TextBlock */
			editor.DataContext = textBlock;

			/* Set MaxHeight and MaxWidth so that editor doesn't cross the boundaries of the control */
			editor.Bind(Control.WidthProperty, new Binding("Bounds.Width"));
			editor.Bind(Control.HeightProperty, new Binding("Bounds.Height"));

			/* Hides the TextBlock in control because of some minor offset in placement, overlaping makes text look fuzzy */
			textBlock.IsVisible = false;
			AdornerPanel.SetPlacement(editor, placement);

			RemoveBorder(); // Remove the highlight border.
		}

		/// <summary>
		/// Aborts the editing. This aborts the underlying change group of the editor
		/// </summary>
		public void AbortEdit()
		{
			editor.AbortEditing();
		}

		/// <summary>
		/// Starts editing once again. This aborts the underlying change group of the editor
		/// </summary>
		public void StartEdit()
		{
			editor.StartEditing();
		}

		#region MouseEvents
		DesignPanelHitTestResult result;
		Point Current;
		Point Start;

		void MouseDown(object sender, PointerEventArgs e)
		{
			result = designPanel.HitTest(e.GetPosition(designPanel as Visual), false, true, HitTestType.Default);
			if (result.ModelHit == ExtendedItem && result.VisualHit is TextBlock)
			{
				Start = e.GetPosition(null);
				Current = Start;
				isMouseDown = true;
			}
			numClicks++;
		}

		void MouseMove(object sender, PointerEventArgs e)
		{
			var pos = e.GetPosition(null);
			Current = pos;
			result = designPanel.HitTest(e.GetPosition(designPanel as Visual), false, true, HitTestType.Default);
			if (result.ModelHit == ExtendedItem && result.VisualHit is TextBlock)
			{
				if (numClicks > 0)
				{
					if (isMouseDown &&
						(Math.Abs(Current.X - Start.X) > 4.0
						 || Math.Abs(Current.Y - Start.Y) > 4.0))
					{
						isGettingDragged = true;
						editor.Focus();
					}
				}
				DrawBorder((Control)result.VisualHit);
			}
			else {
				RemoveBorder();
			}
		}

		void MouseUp(object sender, PointerEventArgs e)
		{
			result = designPanel.HitTest(e.GetPosition(designPanel as Visual), true, true, HitTestType.Default);
			if (((result.ModelHit == ExtendedItem && result.VisualHit is TextBlock) || (result.VisualHit != null && result.VisualHit.TryFindParent<InPlaceEditor>() == editor)) && numClicks > 0)
			{
				if (!isGettingDragged) {
					PlaceEditor(ExtendedItem.View, e);
					foreach (var extension in ExtendedItem.Extensions)
					{
						if (!(extension is InPlaceEditorExtension) && !(extension is SelectedElementRectangleExtension)) {
							ExtendedItem.RemoveExtension(extension);
						}
					}
					editor.IsVisible = true;
				}
			}
			else { // Clicked outside the Text - > hide the editor and make the actual text visible again
				RemoveEventsAndShowControl();
				this.ExtendedItem.ReapplyAllExtensions();
			}

			isMouseDown = false;
			isGettingDragged = false;
		}

		#endregion

		#region HighlightBorder
		private Border _border;
		private sealed class BorderPlacement : AdornerPlacement
		{
			private readonly Control _element;

			public BorderPlacement(Control element)
			{
				_element = element;
			}

			public override void Arrange(AdornerPanel panel, Control adorner, Size adornedElementSize)
			{
				var p = _element.TranslatePoint(new Point(), panel.AdornedElement);
				if (p.HasValue)
				{
					var rect = new Rect(p.Value, _element.Bounds.Size).Inflate(new Thickness(3, 1, 3, 1));
					adorner.Arrange(rect);
				}
			}
		}

		private void DrawBorder(Control item)
		{
			if (editor != null && !editor.IsVisible)
			{
				if (adornerPanel.Children.Contains(_border))
					adornerPanel.Children.Remove(_border);
				_border = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1.4) };
				ToolTip.SetTip(_border, "Edit this Text");
				var bp = new BorderPlacement(item);
				AdornerPanel.SetPlacement(_border, bp);
				adornerPanel.Children.Add(_border);
			}
		}

		private void RemoveBorder()
		{
			if (adornerPanel.Children.Contains(_border))
				adornerPanel.Children.Remove(_border);
		}
		#endregion

		protected override void OnRemove()
		{
			RemoveEventsAndShowControl();
			base.OnRemove();
		}

		private bool eventsAdded;

		private void RemoveEventsAndShowControl()
		{
			editor.IsVisible = false;

			if (textBlock != null)
			{
				textBlock.IsVisible = true;
			}

			if (eventsAdded) {
				eventsAdded = false;
				ExtendedItem.PropertyChanged -= PropertyChanged;
				designPanel.PointerPressed -= MouseDown;
				(designPanel as Avalonia.Controls.Control)?.RemoveHandler(Avalonia.Input.InputElement.PointerMovedEvent, (EventHandler<PointerEventArgs>)MouseMove);
				designPanel.PointerReleased -= MouseUp;
			}
		}
	}
}
