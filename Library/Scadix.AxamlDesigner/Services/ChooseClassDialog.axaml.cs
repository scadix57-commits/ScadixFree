 

using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Services
{
	public partial class ChooseClassDialog: Window
    {
		public ChooseClassDialog()
		{
			InitializeComponent();
		}

		public ChooseClassDialog(ChooseClass core)
		{
			DataContext = core;
			InitializeComponent();
			
			uxFilter.Focus();
			uxList.DoubleTapped += uxList_DoubleTapped;
			uxOk.Click += delegate { Ok(); };
		}
		
		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.Key == Key.Enter) {
				Ok();
				e.Handled = true;
			} else if (e.Key == Key.Up) {
				uxList.SelectedIndex = Math.Max(0, uxList.SelectedIndex - 1);
				e.Handled = true;
			} else if (e.Key == Key.Down) {
				uxList.SelectedIndex++;
				e.Handled = true;
			}
		}
		
		void uxList_DoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var f = (e.Source as Control)?.DataContext as Type;
			if (f != null) {
				Ok();
			}
		}
		
		void Ok()
		{
			Close(true);
		}
	}
	
	class ClassListBox : ListBox
	{
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			SelectedIndex = 0;
			ScrollIntoView(SelectedItem);
			SelectionChanged += (s, args) => ScrollIntoView(SelectedItem);
		}
	}	
	public class ClassNameConverter : IValueConverter
	{
		public static ClassNameConverter Instance = new ClassNameConverter();
		
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var c = value as Type;
			if (c == null) return value;
			return c.Name + " (" + c.Namespace + ")";
		}
		
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
	
	public class NullToBoolConverter : IValueConverter
	{
		public static NullToBoolConverter Instance = new NullToBoolConverter();
		
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null ? false : true;
		}
		
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
