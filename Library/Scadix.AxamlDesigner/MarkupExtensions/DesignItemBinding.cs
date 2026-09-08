

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using System.Globalization;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.UIExtensions;

[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Scadix.AxamlDesigner.MarkupExtensions")]


namespace Scadix.AxamlDesigner.MarkupExtensions
{
	/// <summary>
	/// A Binding to a DesignItem of Object
	/// 
	/// This can be used for Example your own Property Pages for Designer Objects
	/// </summary>
	public class DesignItemBinding : MarkupExtension
	{
		private string _propertyName;
		private AvaloniaProperty _property;
		private Binding _binding;
		private DesignItemSetConverter _converter;
		private AvaloniaProperty _targetProperty;
		private Control _targetObject;

		public bool SingleItemProperty { get; set; }
		
		public bool AskWhenMultipleItemsSelected { get; set; }
		
		public IValueConverter Converter { get; set; }
		
		public object ConverterParameter { get; set; }
		
		public UpdateSourceTrigger UpdateSourceTrigger { get; set; }

		public UpdateSourceTrigger? UpdateSourceTriggerMultipleSelected { get; set; }

		public DesignItemBinding(string path)
		{
			this._propertyName = path;
			
			UpdateSourceTrigger = UpdateSourceTrigger.Default;
			AskWhenMultipleItemsSelected = true;
		}

		public DesignItemBinding(AvaloniaProperty property)
		{
			this._property = property;

			UpdateSourceTrigger = UpdateSourceTrigger.Default;
			AskWhenMultipleItemsSelected = true;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			IProvideValueTarget service = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			_targetObject = service.TargetObject as Control;
			_targetProperty = service.TargetProperty as AvaloniaProperty;

			if (_targetObject != null)
			{
				_targetObject.DataContextChanged += targetObject_DataContextChanged;
			}

			return null;
		}

		public void CreateBindingOnProperty(AvaloniaProperty targetProperty, Control targetObject)
		{
			_targetProperty = targetProperty;
			_targetObject = targetObject;
			_targetObject.DataContextChanged += targetObject_DataContextChanged;
			targetObject_DataContextChanged(_targetObject, null);
		}
		
		void targetObject_DataContextChanged(object sender, EventArgs e)
		{
			var dcontext = ((Control) sender).DataContext;
			
			DesignContext context = null;
			Control fe = null;
			DesignItem designItem = null;
			
			if (dcontext is DesignItem) {
				designItem = (DesignItem)dcontext;
				context = designItem.Context;
				fe = designItem.View as Control;
			} else if (dcontext is Control) {
				fe = ((Control)dcontext);
				var srv = fe.TryFindParent<DesignSurface>();
				if (srv != null) {
					context = srv.DesignContext;
					designItem = context.Services.Component.GetDesignItem(fe);
				}
			}

			if (context != null)
			{
				if (_property != null)
				{
					_binding = new Binding();
					_binding.Path = _property.Name;
					_binding.Source = fe;
					_binding.UpdateSourceTrigger = UpdateSourceTrigger;

					if (designItem.Services.Selection.SelectedItems.Count > 1 && UpdateSourceTriggerMultipleSelected != null)
					{
						_binding.UpdateSourceTrigger = UpdateSourceTriggerMultipleSelected.Value;
					}

					_binding.Mode = BindingMode.TwoWay;
					_binding.ConverterParameter = ConverterParameter;

					_converter = new DesignItemSetConverter(designItem, _property, SingleItemProperty, AskWhenMultipleItemsSelected,
						Converter);
					_binding.Converter = _converter;

					_targetObject.Bind(_targetProperty, _binding);
				}
				else
				{
					_binding = new Binding(_propertyName);
					_binding.Source = fe;
					_binding.UpdateSourceTrigger = UpdateSourceTrigger;

					if (designItem.Services.Selection.SelectedItems.Count > 1 && UpdateSourceTriggerMultipleSelected != null)
					{
						_binding.UpdateSourceTrigger = UpdateSourceTriggerMultipleSelected.Value;
					}

					_binding.Mode = BindingMode.TwoWay;
					_binding.ConverterParameter = ConverterParameter;

					_converter = new DesignItemSetConverter(designItem, _propertyName, SingleItemProperty, AskWhenMultipleItemsSelected,
						Converter);
					_binding.Converter = _converter;

					_targetObject.Bind(_targetProperty, _binding);
				}
			}
			else
			{
				_targetObject.ClearValue(_targetProperty);
			}
		}

		private class DesignItemSetConverter : IValueConverter
		{
			private DesignItem _designItem;
			private string _propertyName;
			private AvaloniaProperty _property;
			private bool _singleItemProperty;
			private bool _askWhenMultipleItemsSelected;
			private IValueConverter _converter;

			public DesignItemSetConverter(DesignItem desigItem, string propertyName, bool singleItemProperty, bool askWhenMultipleItemsSelected, IValueConverter converter)
			{
				this._designItem = desigItem;
				this._propertyName = propertyName;
				this._singleItemProperty = singleItemProperty;
				this._converter = converter;
				this._askWhenMultipleItemsSelected = askWhenMultipleItemsSelected;
			}

			public DesignItemSetConverter(DesignItem desigItem, AvaloniaProperty property, bool singleItemProperty, bool askWhenMultipleItemsSelected, IValueConverter converter)
			{
				this._designItem = desigItem;
				this._property = property;
				this._propertyName = property.Name;
				this._singleItemProperty = singleItemProperty;
				this._converter = converter;
				this._askWhenMultipleItemsSelected = askWhenMultipleItemsSelected;
			}

			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				if (_converter != null)
					return _converter.Convert(value, targetType, parameter, culture);
				
				return value;
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				var val = value;
				if (_converter != null)
					val = _converter.ConvertBack(value, targetType, parameter, culture);
				
				var changeGroup = _designItem.OpenGroup("Property: " + _propertyName);

				try {
					DesignItemProperty property = null;

					if (_property != null) {
						try {
							property = _designItem.Properties.GetProperty(_property);
						}
						catch (Exception) {
							property = _designItem.Properties.GetAttachedProperty(_property);
						}
					}
					else {
						property = _designItem.Properties.GetProperty(_propertyName);
					}
					
					property.SetValue(val);

					if (!_singleItemProperty && _designItem.Services.Selection.SelectedItems.Count > 1)
					{
						bool applyToAll = true;
						if (_askWhenMultipleItemsSelected) {
							// MessageBox not available in Avalonia - default to applying to all
							applyToAll = true;
						}
						if (applyToAll)
						{
							foreach (var item in _designItem.Services.Selection.SelectedItems)
							{
								try
								{
									if (_property != null)
										property = item.Properties.GetProperty(_property);
									else
										property = item.Properties.GetProperty(_propertyName);
								}
								catch(Exception)
								{ }
								if (property != null)
									property.SetValue(val);
							}
						}
					}

					changeGroup.Commit();
				}
				catch (Exception)
				{
					changeGroup.Abort();
				}

				return val;
			}
		}
	}
}
