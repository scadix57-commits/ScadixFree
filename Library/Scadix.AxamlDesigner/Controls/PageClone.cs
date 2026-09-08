
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using System.ComponentModel;
using System.Diagnostics;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDom;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// Description of PageClone.
	/// </summary>
	public class PageClone : TemplatedControl, IAddChild
    {
		public static readonly StyledProperty<object> ContentProperty =
			AvaloniaProperty.Register<PageClone, object>("Content");

		[Category("Appearance")]
		public new Brush Background {
			get { return (Brush)GetValue(BackgroundProperty); }
			set { SetValue(BackgroundProperty, value); }
		}

		public object Content {
			get { return GetValue(ContentProperty); }
			set { SetValue(ContentProperty, value); }
		}

		[Bindable(true), Category("Appearance")]
		public new FontFamily FontFamily {
			get { return (FontFamily)GetValue(FontFamilyProperty); }
			set { SetValue(FontFamilyProperty, value); }
		}

		[Bindable(true), Category("Appearance")]
		public new double FontSize {
			get { return (double)GetValue(FontSizeProperty); }
			set { SetValue(FontSizeProperty, value); }
		}

		[Bindable(true), Category("Appearance")]
		public new Brush Foreground {
			get { return (Brush)GetValue(ForegroundProperty); }
			set { SetValue(ForegroundProperty, value); }
		}

		public bool ShowsNavigationUI { get; set; }

		public string Title { get; set; }

		public string WindowTitle {
			get { return Title; }
			set { Title = value; }
		}

		public double WindowWidth { get; set; }

		public double WindowHeight { get; set; }

		public  void AddChild(object value)
		{
			base.VerifyAccess();
			if (this.Content == null || value == null)
				this.Content = value;
			else
				throw new InvalidOperationException();
		}
		
		void IAddChild.AddText(string text)
        {
			if (text == null)
				return;
			
			for (int i = 0; i < text.Length; i++) {
				if (!char.IsWhiteSpace(text[i]))
					throw new ArgumentException();
			}
        }
    }

	/// <summary>
	/// A <see cref="CustomInstanceFactory"/> for Page
	/// (and derived classes, unless they specify their own <see cref="CustomInstanceFactory"/>).
	/// </summary>
	public class PageCloneExtension : CustomInstanceFactory
	{
		/// <summary>
		/// Used to create instances of <see cref="PageClone"/>.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			Debug.Assert(arguments.Length == 0);
			return new PageClone();
		}
	}
}
