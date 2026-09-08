

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Scadix.AxamlDom.Converters;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace Scadix.AxamlDom
{
	/// <summary>
	/// Represents a property assignable in XAML.
	/// This can be a normal .NET property or an attached property.
	/// </summary>
	internal abstract class XamlPropertyInfo
	{
		public abstract object GetValue(object instance);
		public abstract void SetValue(object instance, object value);
		public abstract void ResetValue(object instance);
		public abstract TypeConverter TypeConverter { get; }
		public abstract Type TargetType { get; }
		public abstract Type ReturnType { get; }
		public abstract string Name { get; }
		public abstract string FullyQualifiedName { get; }
		public abstract bool IsAttached { get; }
		public abstract bool IsCollection { get; }
		public virtual bool IsEvent { get { return false; } }
		public virtual bool IsAdvanced { get { return false; } }
		public virtual AvaloniaProperty DependencyProperty { get { return null; } }
		public abstract string Category { get; }
	}
	
	#region XamlDependencyPropertyInfo
	internal class XamlDependencyPropertyInfo : XamlPropertyInfo
	{
		readonly AvaloniaProperty property;
		readonly bool isAttached;
		readonly bool isCollection;
		readonly string dependencyPropertyGetterName;
		Func<object, object> attachedGetter;

		public override AvaloniaProperty DependencyProperty {
			get { return property; }
		}
		
		public XamlDependencyPropertyInfo(AvaloniaProperty property, bool isAttached, string dependencyPropertyGetterName, Func<object, object> attachedGetter = null)
		{
			Debug.Assert(property != null);
			this.property = property;
			this.isAttached = isAttached;
			this.isCollection = CollectionSupport.IsCollectionType(property.PropertyType);
			this.attachedGetter = attachedGetter;
			this.dependencyPropertyGetterName = dependencyPropertyGetterName;
		}

		public override TypeConverter TypeConverter {
			get {
				return TypeDescriptor.GetConverter(this.ReturnType);
			}
		}

        public override string FullyQualifiedName {
			get {
				return this.TargetType.FullName + "." + this.Name;
			}
		}
		
		public override Type TargetType {
			get { return property.OwnerType; }
		}
		
		public override Type ReturnType {
			get { return property.PropertyType; }
		}
		
		public override string Name {
			get { return dependencyPropertyGetterName; }
		}

		public override string Category {
			get { return "Misc"; }
		}
		
		public override bool IsAttached {
			get { return isAttached; }
		}
		
		public override bool IsCollection {
			get {
				return isCollection;
			}
		}
		
		public override object GetValue(object instance)
		{
			try {
				if (attachedGetter != null) {
					return attachedGetter(instance);
				}
			} catch (Exception) {
				
			}

			var dependencyObject = instance as AvaloniaObject;
			if (dependencyObject != null) {
				return dependencyObject.GetValue(property);
			}

			return null;
		}
		
		public override void SetValue(object instance, object value)
		{
			var avaloniaObject = (AvaloniaObject)instance;
			if (value is Avalonia.Data.IBinding binding)
				avaloniaObject.Bind(property, binding);
			else
				avaloniaObject.SetValue(property, value);
		}
		
		public override void ResetValue(object instance)
		{
			((AvaloniaObject)instance).ClearValue(property);
		}
	}
	#endregion
	
	#region XamlNormalPropertyInfo
	internal sealed class XamlNormalPropertyInfo : XamlPropertyInfo
	{
		PropertyDescriptor _propertyDescriptor;
        AvaloniaProperty dependencyProperty;
		
		public XamlNormalPropertyInfo(PropertyDescriptor propertyDescriptor)
		{
            this._propertyDescriptor = propertyDescriptor;
            var dpd = AvaloniaPropertyRegistry.Instance.FindRegistered(propertyDescriptor.ComponentType, propertyDescriptor.Name); ;
            if (dpd != null)
            {
                dependencyProperty = dpd;
            }
        }

		public override AvaloniaProperty DependencyProperty {
			get {
				return dependencyProperty;
			}
		}
		
		public override object GetValue(object instance)
		{
			return _propertyDescriptor.GetValue(instance);
		}
		
		public override void SetValue(object instance, object value)
		{
			if (value is Avalonia.Data.IBinding binding && instance is AvaloniaObject avaloniaObject && dependencyProperty != null)
				avaloniaObject.Bind(dependencyProperty, binding);
			else
				_propertyDescriptor.SetValue(instance, value);
		}
		
		public override void ResetValue(object instance)
		{
			try {
				_propertyDescriptor.ResetValue(instance);
			} catch (Exception) {
				//For Example "UndoRedoSimpleBinding" will raise a exception here => look if it has Side Effects if we generally catch here?
			}
		}
		
		public override Type ReturnType {
			get { return _propertyDescriptor.PropertyType; }
		}
		
		public override Type TargetType {
			get { return _propertyDescriptor.ComponentType; }
		}
		
		public override string Category {
			get { return _propertyDescriptor.Category; }
		}
		
		public override TypeConverter TypeConverter {
			get {
				return GetCustomTypeConverter(_propertyDescriptor.PropertyType) ?? _propertyDescriptor.Converter;
			}
		}
		
		public override string FullyQualifiedName {
			get {
				return _propertyDescriptor.ComponentType.FullName + "." + _propertyDescriptor.Name;
			}
		}
		
		public override string Name {
			get { return _propertyDescriptor.Name; }
		}
		
		public override bool IsAttached {
			get { return false; }
		}
		
		public override bool IsCollection {
			get {
				return CollectionSupport.IsCollectionType(_propertyDescriptor.PropertyType);
			}
		}

		public override bool IsAdvanced	{
			get	{
				var a = _propertyDescriptor.Attributes[typeof(EditorBrowsableAttribute)] as EditorBrowsableAttribute;
				if (a != null) {
					return a.State == EditorBrowsableState.Advanced;
				}
				return false;
			}
		}
		
		public static readonly TypeConverter StringTypeConverter = TypeDescriptor.GetConverter(typeof(string));

		public static TypeConverter GetCustomTypeConverter(Type propertyType)
		{
			if (propertyType == typeof(object))
				return StringTypeConverter;
			else if (propertyType == typeof(Type))
				return TypeTypeConverter.Instance;
            else if (typeof(AvaloniaProperty).IsAssignableFrom(propertyType)) return DependencyPropertyConverter.Instance;
            else
				return null;
		}
		#region Static Helper Methods

		//public static TypeConverter GetCustomTypeConverter(Type propertyType)
		//{
		//    if (propertyType == typeof(object)) return StringTypeConverter;
		//    if (propertyType == typeof(Type)) return TypeTypeConverter.Instance;
		//    if (typeof(AvaloniaProperty).IsAssignableFrom(propertyType)) return DependencyPropertyConverter.Instance;

		//    // Layout & Geometry
		//    if (propertyType == typeof(Thickness)) return new Converters.ThicknessConverter();
		//    if (propertyType == typeof(CornerRadius)) return new Converters.CornerRadiusConverter();
		//    if (propertyType == typeof(GridLength)) return new Converters.GridLengthConverter();
		//    if (propertyType == typeof(Point)) return new Converters.PointConverter();
		//    if (propertyType == typeof(Size)) return new Converters.SizeConverter();
		//    if (propertyType == typeof(Rect)) return new Converters.RectConverter();

		//    // Color & Brush
		//    if (propertyType == typeof(Color)) return new Converters.ColorConverter();
		//    if (typeof(IBrush).IsAssignableFrom(propertyType)) return new BrushConverter();
		//    if (typeof(IImage).IsAssignableFrom(propertyType)) return new Converters.BitmapConverter();
		//    if (propertyType == typeof(WindowIcon)) return new Converters.IconConverter();
		//    if (propertyType == typeof(Avalonia.Media.BoxShadows)) return new BoxShadowsConverter();

		//    // Font
		//    if (propertyType == typeof(FontWeight)) return new Converters.FontWeightConverter();
		//    if (propertyType == typeof(FontStyle)) return new Converters.FontStyleConverter();
		//    if (propertyType == typeof(FontStretch)) return new Converters.FontStretchConverter();

		//    // Input & Transform
		//    if (propertyType == typeof(Cursor)) return new Converters.CursorConverter();
		//    if (propertyType == typeof(Matrix)) return new Converters.MatrixConverter();
		//    if (typeof(Transform).IsAssignableFrom(propertyType)) return new TransformConverter();
		//    if (typeof(Geometry).IsAssignableFrom(propertyType)) return new Converters.GeometryConverter();

		//    // Other Avalonia Types
		//    if (propertyType == typeof(RelativePoint)) return new Converters.RelativePointConverter();
		//    if (propertyType == typeof(Cue)) return new Converters.CueConverter();
		//    if (propertyType == typeof(KeySpline)) return new Converters.KeySplineConverter();
		//    if (propertyType == typeof(IterationCount)) return new Converters.IterationCountConverter();
		//    if (propertyType == typeof(FontFamily)) return new Converters.FontFamilyConverter();
		//    if (propertyType == typeof(Avalonia.Styling.Selector)) return new Converters.SelectorConverter();
		//    if (propertyType.Name == "CompiledBindingPath") return new Converters.CompiledBindingPathConverter(propertyType);

		//    // Grid
		//    if (propertyType == typeof(ColumnDefinitions)) return new Converters.ColumnDefinitionsConverter();
		//    if (propertyType == typeof(RowDefinitions)) return new Converters.RowDefinitionsConverter();
		//    if (propertyType == typeof(WindowStartupLocation)) return new EnumConverter(typeof(WindowStartupLocation));

		//    // Documents
		//    if (propertyType == typeof(Avalonia.Controls.Documents.Inline)) return new Converters.InlineConverter();

		//    return null;
		//}

		#endregion
		sealed class TypeTypeConverter : TypeConverter
		{
			public readonly static TypeTypeConverter Instance = new TypeTypeConverter();
			
			public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
			{
				if (sourceType == typeof(string))
					return true;
				else
					return base.CanConvertFrom(context, sourceType);
			}
			
			public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			{
				if (value == null)
					return null;
				if (value is string) {
					IXamlTypeResolver xamlTypeResolver = (IXamlTypeResolver)context.GetService(typeof(IXamlTypeResolver));
					if (xamlTypeResolver == null)
						throw new XamlLoadException("IXamlTypeResolver not found in type descriptor context.");
					return xamlTypeResolver.Resolve((string)value);
				} else {
					return base.ConvertFrom(context, culture, value);
				}
			}
		}
		
		sealed class DependencyPropertyConverter : TypeConverter
		{
			public readonly static DependencyPropertyConverter Instance = new DependencyPropertyConverter();
			
			public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
			{
				if (sourceType == typeof(string))
					return true;
				else
					return base.CanConvertFrom(context, sourceType);
			}
			
			public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			{
				if (value == null)
					return null;
				if (value is string) {
					XamlTypeResolverProvider xamlTypeResolver = (XamlTypeResolverProvider)context.GetService(typeof(XamlTypeResolverProvider));
					if (xamlTypeResolver == null)
						throw new XamlLoadException("XamlTypeResolverProvider not found in type descriptor context.");
					XamlPropertyInfo prop = xamlTypeResolver.ResolveProperty((string)value);
					if (prop == null)
						throw new XamlLoadException("Could not find property " + value + ".");
					XamlDependencyPropertyInfo depProp = prop as XamlDependencyPropertyInfo;
					if (depProp != null)
						return depProp.DependencyProperty;
					FieldInfo field = prop.TargetType.GetField(prop.Name + "Property", BindingFlags.Public | BindingFlags.Static);
                    if (field != null && typeof(AvaloniaProperty).IsAssignableFrom(field.FieldType))
                    {
						return (AvaloniaProperty)field.GetValue(null);
					}
					throw new XamlLoadException("Property " + value + " is not a dependency property.");
				} else {
					return base.ConvertFrom(context, culture, value);
				}
			}
		}
	}

	
    #endregion
	 
        #region XamlEventPropertyInfo
    sealed class XamlEventPropertyInfo : XamlPropertyInfo
	{
		readonly EventDescriptor _eventDescriptor;
		
		public XamlEventPropertyInfo(EventDescriptor eventDescriptor)
		{
			this._eventDescriptor = eventDescriptor;
		}
		
		public override object GetValue(object instance)
		{
			throw new NotSupportedException();
		}
		
		public override void SetValue(object instance, object value)
		{
			
		}
		
		public override void ResetValue(object instance)
		{
			
		}
		
		public override Type ReturnType {
			get { return _eventDescriptor.EventType; }
		}
		
		public override Type TargetType {
			get { return _eventDescriptor.ComponentType; }
		}
		
		public override string Category {
			get { return _eventDescriptor.Category; }
		}
		
		public override TypeConverter TypeConverter {
			get { return XamlNormalPropertyInfo.StringTypeConverter; }
		}
		
		public override string FullyQualifiedName {
			get {
				return _eventDescriptor.ComponentType.FullName + "." + _eventDescriptor.Name;
			}
		}
		
		public override string Name {
			get { return _eventDescriptor.Name; }
		}
		
		public override bool IsEvent {
			get { return true; }
		}
		
		public override bool IsAttached {
			get { return false; }
		}
		
		public override bool IsCollection {
			get { return false; }
		}
	}
	#endregion
}
