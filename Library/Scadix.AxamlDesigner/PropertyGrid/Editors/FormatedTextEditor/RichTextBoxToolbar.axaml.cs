 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors.FormatedTextEditor
{
	/// <summary>
	/// Interaktionslogik für RichTextBoxToolbar.xaml
	/// </summary>
	public partial class RichTextBoxToolbar : UserControl
	{
		public RichTextBoxToolbar()
		{
			InitializeComponent();
		}

		public void SetValuesFromTextBlock(TextBlock textBlock)
		{
			if (cmbFontFamily != null)
				cmbFontFamily.Text = textBlock.FontFamily?.ToString();
			if (cmbFontSize != null)
				cmbFontSize.Text = textBlock.FontSize.ToString();
		}

		void BoldButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) { }
		void ItalicButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) { }
		void UnderlineButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) { }

		public Control RichTextBox
		{
			get { return GetValue(RichTextBoxProperty); }
			set { SetValue(RichTextBoxProperty, value); }
		}

		public static readonly StyledProperty<Control> RichTextBoxProperty =
			AvaloniaProperty.Register<RichTextBoxToolbar, Control>("RichTextBox");
	}
}
