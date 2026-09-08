

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Reflection;
using System.Windows.Input;


namespace Scadix.AxamlDesigner
{
	public class CallExtension : MarkupExtension
	{
		public CallExtension(string methodName)
		{
			this.methodName = methodName;
		}

		string methodName;

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var t = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			return new CallCommand(t.TargetObject as Control, methodName);
		}
	}

	public class CallCommand : AvaloniaObject, ICommand
	{
		public CallCommand(Control element, string methodName)
		{
			this.element = element;
			this.methodName = methodName;
			element.DataContextChanged += target_DataContextChanged;
			var bnd = new Binding("DataContext.Can" + methodName) { Source = element };
			this.Bind(CanCallProperty, bnd);
		}

        private void target_DataContextChanged(object? sender, EventArgs e)
        {
            GetMethod();
            RaiseCanExecuteChanged();
        }

        Control element;
		string methodName;
		MethodInfo method;

		public static readonly AvaloniaProperty<bool> CanCallProperty =
			AvaloniaProperty.Register< CallCommand,bool >("CanCall",true);
			                            

		public bool CanCall {
			get { return (bool)GetValue(CanCallProperty); }
			set { SetValue(CanCallProperty, value); }
		}

		public object DataContext {
			get { return element.DataContext; }
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.Property == CanCallProperty) {
				RaiseCanExecuteChanged();
			}
		}

		void GetMethod()
		{
			if (DataContext == null) {
				method = null;
			}
			else {
				method = DataContext.GetType().GetMethod(methodName, Type.EmptyTypes);
			}
		}

	 

		void RaiseCanExecuteChanged()
		{
			if (CanExecuteChanged != null) {
				CanExecuteChanged(this, EventArgs.Empty);
			}
		}

		#region ICommand Members

		public event EventHandler CanExecuteChanged;

		public bool CanExecute(object parameter)
		{
			return method != null && CanCall;
		}

		public void Execute(object parameter)
		{
			method.Invoke(DataContext, null);
		}

		#endregion
	}
}
