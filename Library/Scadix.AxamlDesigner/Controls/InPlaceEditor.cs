 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.PropertyGrid.Editors.FormatedTextEditor;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// Supports editing Text in the Designer
	/// </summary>
	public class InPlaceEditor : TemplatedControl
	{
		protected override Type StyleKeyOverride => typeof(InPlaceEditor);
	 
		/// <summary>
		/// This property is binded to the Text Property of the editor.
		/// </summary>
		public static readonly StyledProperty<string> BindProperty =
			AvaloniaProperty.Register<InPlaceEditor, string>("Bind");

		public string Bind
		{
			get { return (string)GetValue(BindProperty); }
			set { SetValue(BindProperty, value); }
		}

		private readonly DesignItem designItem;
		private ChangeGroup changeGroup;
		private TextBox editor;

		bool _isChangeGroupOpen;

		public InPlaceEditor(DesignItem designItem)
		{
			this.designItem = designItem;
		}
		
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			editor = e.NameScope.Find<TextBox>("PART_Editor");
			if (editor != null)
			{
				editor.KeyDown += delegate (object sender, KeyEventArgs ke)
				{
					if (ke.Key == Key.Enter && (ke.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.Shift)
					{
						ke.Handled = true;
					}
				};
				ToolTip.SetTip(this, "Edit the Text. Press" + Environment.NewLine + "Enter to make changes." + Environment.NewLine + "Shift+Enter to insert a newline." + Environment.NewLine + "Esc to cancel editing.");

			 
			FormatedTextEditor.SetRichTextBoxTextFromTextBlock(editor, ((TextBlock)designItem.Component));
				editor.TextChanged += editor_TextChanged;
			}
		}

		void editor_TextChanged(object sender, TextChangedEventArgs e)
		{
			FormatedTextEditor.SetTextBlockTextFromRichTextBlox(this.designItem, editor);
		}

		protected override void OnGotFocus(GotFocusEventArgs e)
		{
			base.OnGotFocus(e);
			StartEditing();
		}

		/// <summary>
		/// Change is committed if the user releases the Escape Key.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			base.OnKeyUp(e);
			if ((e.KeyModifiers & KeyModifiers.Shift) == 0)
			{
				switch (e.Key)
				{
					case Key.Enter:
						// Commit the changes to DOM.
						if (designItem.Properties[TemplatedControl.FontFamilyProperty].GetConvertedValueOnInstance<FontFamily>() != editor.FontFamily)
							designItem.Properties[TemplatedControl.FontFamilyProperty].SetValue(editor.FontFamily);
						if (designItem.Properties[TemplatedControl.FontSizeProperty].GetConvertedValueOnInstance<double>() != editor.FontSize)
							designItem.Properties[TemplatedControl.FontSizeProperty].SetValue(editor.FontSize);
						if (designItem.Properties[TemplatedControl.FontStretchProperty].GetConvertedValueOnInstance<FontStretch>() != editor.FontStretch)
							designItem.Properties[TemplatedControl.FontStretchProperty].SetValue(editor.FontStretch);
						if (designItem.Properties[TemplatedControl.FontStyleProperty].GetConvertedValueOnInstance<FontStyle>() != editor.FontStyle)
							designItem.Properties[TemplatedControl.FontStyleProperty].SetValue(editor.FontStyle);
						if (designItem.Properties[TemplatedControl.FontWeightProperty].GetConvertedValueOnInstance<FontWeight>() != editor.FontWeight)
							designItem.Properties[TemplatedControl.FontWeightProperty].SetValue(editor.FontWeight);

						if (changeGroup != null && _isChangeGroupOpen)
						{
							FormatedTextEditor.SetTextBlockTextFromRichTextBlox(this.designItem, editor);
							changeGroup.Commit();
							_isChangeGroupOpen = false;
						}
						changeGroup = null;
						this.IsVisible = false;
						this.designItem.ReapplyAllExtensions();
						((TextBlock)designItem.Component).IsVisible = true;
						break;
					case Key.Escape:
						AbortEditing();
						break;
				}
			}
			else if (e.Key == Key.Enter && editor != null)
			{
				editor.Text += Environment.NewLine;
			}
		}

		public void AbortEditing()
		{
			if (changeGroup != null && _isChangeGroupOpen)
			{
				changeGroup.Abort();
				_isChangeGroupOpen = false;
			}
			this.IsVisible = false;
		}

		public void StartEditing()
		{
			if (changeGroup == null)
			{
				changeGroup = designItem.OpenGroup("Change Text");
				_isChangeGroupOpen = true;
			}
			this.IsVisible = true;
		}
	}
}
