

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.UIExtensions;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors.FormatedTextEditor
{
	/// <summary>
	/// Formatted text editor for TextBlock content.
	/// </summary>
	public partial class FormatedTextEditor: UserControl
	{
		private DesignItem designItem;
		private TextBox richTextBox;

		public FormatedTextEditor(DesignItem designItem)
		{
			InitializeComponent();

			this.designItem = designItem;
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			richTextBox = e.NameScope.Find<TextBox>("PART_Editor");
			if (richTextBox != null && designItem?.Component is TextBlock tb)
			{
				richTextBox.Text = tb.Text;
			}
		}

		/// <summary>
		/// Sets the text of a TextBox from a TextBlock's text content.
		/// </summary>
		public static void SetRichTextBoxTextFromTextBlock(TextBox textBox, TextBlock textBlock)
		{
			if (textBox == null || textBlock == null) return;
			textBox.Text = textBlock.Text ?? string.Empty;
		}

		/// <summary>
		/// Fixes InitializeComponent with multiple Versions of same Assembly loaded
		/// </summary>
		public void SpecialInitializeComponent()
		{
			this.InitializeComponent();
		}

		private static Inline CloneInline(Inline inline)
		{
			Inline retVal = null;
			if (inline is LineBreak)
				retVal = new LineBreak();
			else if (inline is Span)
				retVal = new Span();
			else if (inline is Run run)
				retVal = new Run(run.Text);

			if (retVal == null) return null;

			if (inline.IsSet(Inline.BackgroundProperty))
				retVal.Background = inline.Background;
			if (inline.IsSet(Inline.ForegroundProperty))
				retVal.Foreground = inline.Foreground;
			if (inline.IsSet(Inline.FontFamilyProperty))
				retVal.FontFamily = inline.FontFamily;
			if (inline.IsSet(Inline.FontSizeProperty))
				retVal.FontSize = inline.FontSize;
			if (inline.IsSet(Inline.FontStretchProperty))
				retVal.FontStretch = inline.FontStretch;
			if (inline.IsSet(Inline.FontStyleProperty))
				retVal.FontStyle = inline.FontStyle;
			if (inline.IsSet(Inline.FontWeightProperty))
				retVal.FontWeight = inline.FontWeight;
			if (inline.IsSet(Inline.TextDecorationsProperty))
				retVal.TextDecorations = inline.TextDecorations;

			return retVal;
		}

		private static DesignItem InlineToDesignItem(DesignItem designItem, Inline inline)
		{
			var cloned = CloneInline(inline);
			if (cloned == null) return null;

			DesignItem d = designItem.Services.Component.RegisterComponentForDesigner(cloned);
			if (inline is Run run && run.IsSet(Run.TextProperty))
			{
				d.Properties.GetProperty(Run.TextProperty).SetValue(run.Text);
			}

			SetDesignItemTextpropertiesFromInline(d, inline);
			return d;
		}

		private static void SetDesignItemTextpropertiesFromInline(DesignItem targetDesignItem, Inline inline)
		{
			if (inline.IsSet(TextElement.BackgroundProperty))
				targetDesignItem.Properties.GetProperty(TextElement.BackgroundProperty).SetValue(inline.Background);
			if (inline.IsSet(TextElement.ForegroundProperty))
				targetDesignItem.Properties.GetProperty(TextElement.ForegroundProperty).SetValue(inline.Foreground);
			if (inline.IsSet(TextElement.FontFamilyProperty))
				targetDesignItem.Properties.GetProperty(TextElement.FontFamilyProperty).SetValue(inline.FontFamily);
			if (inline.IsSet(TextElement.FontSizeProperty))
				targetDesignItem.Properties.GetProperty(TextElement.FontSizeProperty).SetValue(inline.FontSize);
			if (inline.IsSet(TextElement.FontStretchProperty))
				targetDesignItem.Properties.GetProperty(TextElement.FontStretchProperty).SetValue(inline.FontStretch);
			if (inline.IsSet(TextElement.FontStyleProperty))
				targetDesignItem.Properties.GetProperty(TextElement.FontStyleProperty).SetValue(inline.FontStyle);
			if (inline.IsSet(TextElement.FontWeightProperty))
				targetDesignItem.Properties.GetProperty(TextElement.FontWeightProperty).SetValue(inline.FontWeight);
			if (inline.TextDecorations != null && inline.TextDecorations.Count > 0)
			{
				targetDesignItem.Properties.GetProperty("TextDecorations").SetValue(new TextDecorationCollection());
				var tdColl = targetDesignItem.Properties.GetProperty("TextDecorations");
				foreach (var td in inline.TextDecorations)
				{
					var newTd = targetDesignItem.Services.Component.RegisterComponentForDesigner(new TextDecoration());
					if (inline.IsSet(TextDecoration.LocationProperty))
						newTd.Properties.GetProperty(TextDecoration.LocationProperty).SetValue(td.Location);
					// TextDecoration.Pen not available in Avalonia
					tdColl.CollectionElements.Add(newTd);
				}
			}
		}

		/// <summary>
		/// Sets the TextBlock text from the TextBox content.
		/// </summary>
		public static void SetTextBlockTextFromRichTextBlox(DesignItem designItem, TextBox textBox)
		{
			if (textBox == null) return;

			designItem.Properties.GetProperty(TextBlock.TextProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.FontSizeProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.FontFamilyProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.FontStretchProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.FontWeightProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.BackgroundProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.ForegroundProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.FontStyleProperty).Reset();
			designItem.Properties.GetProperty(TextBlock.TextDecorationsProperty).Reset();

			designItem.Properties.GetProperty(TextBlock.TextProperty).SetValue(textBox.Text ?? string.Empty);
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			var changeGroup = designItem.OpenGroup("Formated Text");
			SetTextBlockTextFromRichTextBlox(designItem, richTextBox);
			changeGroup.Commit();
			this.TryFindParent<Window>().Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.TryFindParent<Window>().Close();
		}
	}
}
