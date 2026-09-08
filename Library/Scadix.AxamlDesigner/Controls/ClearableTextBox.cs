
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Scadix.AxamlDesigner.Controls
{
	public class ClearableTextBox : EnterTextBox
	{
		private Button textRemoverButton;

		protected override Type StyleKeyOverride => typeof(ClearableTextBox);

		public ClearableTextBox()
		{
			this.GotFocus += this.TextBoxGotFocus;
			this.LostFocus += this.TextBoxLostFocus;
			this.TextChanged += this.TextBoxTextChanged;
			this.KeyUp += this.ClearableTextBox_KeyUp;
		}

		void ClearableTextBox_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				this.TextRemoverClick(sender, null);
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			this.textRemoverButton =  e.NameScope.Find<Button>("TextRemover") as Button;
			if (null != this.textRemoverButton)
			{
				this.textRemoverButton.Click += this.TextRemoverClick;
			}

			this.UpdateState();
		}

		protected void UpdateState()
		{
            if (string.IsNullOrEmpty(Text))
            {
                PseudoClasses.Remove(":text-remover-visible");
                PseudoClasses.Add(":text-remover-hidden");
            }
            else
            {
                PseudoClasses.Remove(":text-remover-hidden");
                PseudoClasses.Add(":text-remover-visible");
            }
        }

		private void TextBoxTextChanged(object sender, TextChangedEventArgs e)
		{
			this.UpdateState();
		}

		private void TextRemoverClick(object sender, RoutedEventArgs e)
		{
			this.Text = null;
			this.Focus();
		}

		private void TextBoxGotFocus(object sender, RoutedEventArgs e)
		{
			this.UpdateState();
		}

		private void TextBoxLostFocus(object sender, RoutedEventArgs e)
		{
			this.UpdateState();
		}
	}
}
