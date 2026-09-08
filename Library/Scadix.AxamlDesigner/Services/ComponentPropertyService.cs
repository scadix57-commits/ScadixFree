 

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.PropertyGrid;

namespace Scadix.AxamlDesigner.Services
{
	public class ComponentPropertyService : IComponentPropertyService
	{
		protected HashSet<string> IgnoreTypes = new HashSet<string>(new[]
		{
			"DesignerProperties",
			"KeyboardNavigation",
			"RenderOptions",
			"ToolTipService",
		});

		public virtual IEnumerable<MemberDescriptor> GetAvailableProperties(DesignItem designItem)
		{
            return TypeHelper.GetAvailableProperties(designItem.Component)
            .Where(x => !x.Name.Contains(".") || !IgnoreTypes.Contains(x.Name.Split('.')[0]));

            //var props = TypeHelper.GetAvailableProperties(designItem.Component)
            //	.Where(x => !x.Name.Contains(".") || !IgnoreTypes.Contains(x.Name.Split('.')[0]));

            //// In Avalonia, TypeDescriptor does not return attached properties (unlike WPF).
            //// We manually add parent-container attached properties based on the parent type.
            //return props.Concat(GetAttachedProperties(designItem));
        }

		public virtual IEnumerable<MemberDescriptor> GetAvailableEvents(DesignItem designItem)
		{
			return TypeHelper.GetAvailableEvents(designItem.ComponentType);
		}

		public virtual IEnumerable<MemberDescriptor> GetCommonAvailableProperties(IEnumerable<DesignItem> designItems)
		{
			return TypeHelper.GetCommonAvailableProperties(designItems.Select(t => t.Component))
				.Where(x => !x.Name.Contains(".") || !IgnoreTypes.Contains(x.Name.Split('.')[0]));
		}

		/// <summary>
		/// Returns attached property descriptors based on the parent container type.
		/// In WPF these come automatically via TypeDescriptor; in Avalonia we add them manually.
		/// </summary>
		static IEnumerable<PropertyDescriptor> GetAttachedProperties(DesignItem designItem)
		{
			if (designItem.Parent == null) yield break;

			var parentType = designItem.Parent.ComponentType;

			if (parentType == typeof(Canvas))
			{
				yield return new AvaloniaAttachedPropertyDescriptor("Canvas.Left",  typeof(double), designItem.Component,
					c => Canvas.GetLeft((Avalonia.Layout.Layoutable)c),
					(c, v) => Canvas.SetLeft((Avalonia.Layout.Layoutable)c, (double)v));

				yield return new AvaloniaAttachedPropertyDescriptor("Canvas.Top",   typeof(double), designItem.Component,
					c => Canvas.GetTop((Avalonia.Layout.Layoutable)c),
					(c, v) => Canvas.SetTop((Avalonia.Layout.Layoutable)c, (double)v));

				yield return new AvaloniaAttachedPropertyDescriptor("Canvas.Right",  typeof(double), designItem.Component,
					c => Canvas.GetRight((Avalonia.Layout.Layoutable)c),
					(c, v) => Canvas.SetRight((Avalonia.Layout.Layoutable)c, (double)v));

				yield return new AvaloniaAttachedPropertyDescriptor("Canvas.Bottom", typeof(double), designItem.Component,
					c => Canvas.GetBottom((Avalonia.Layout.Layoutable)c),
					(c, v) => Canvas.SetBottom((Avalonia.Layout.Layoutable)c, (double)v));
			}
			else if (parentType == typeof(Grid))
			{
				yield return new AvaloniaAttachedPropertyDescriptor("Grid.Row",      typeof(int), designItem.Component,
					c => Grid.GetRow((Avalonia.Controls.Control)c),
					(c, v) => Grid.SetRow((Avalonia.Controls.Control)c, (int)v));

				yield return new AvaloniaAttachedPropertyDescriptor("Grid.Column",   typeof(int), designItem.Component,
					c => Grid.GetColumn((Avalonia.Controls.Control)c),
					(c, v) => Grid.SetColumn((Avalonia.Controls.Control)c, (int)v));

				yield return new AvaloniaAttachedPropertyDescriptor("Grid.RowSpan",    typeof(int), designItem.Component,
					c => Grid.GetRowSpan((Avalonia.Controls.Control)c),
					(c, v) => Grid.SetRowSpan((Avalonia.Controls.Control)c, (int)v));

				yield return new AvaloniaAttachedPropertyDescriptor("Grid.ColumnSpan", typeof(int), designItem.Component,
					c => Grid.GetColumnSpan((Avalonia.Controls.Control)c),
					(c, v) => Grid.SetColumnSpan((Avalonia.Controls.Control)c, (int)v));
			}
		}
	}

	/// <summary>
	/// A PropertyDescriptor that wraps an Avalonia attached property for display in the PropertyGrid.
	/// </summary>
	public class AvaloniaAttachedPropertyDescriptor : PropertyDescriptor
	{
		readonly Type _propertyType;
		readonly Func<object, object> _getter;
		readonly Action<object, object> _setter;

		public AvaloniaAttachedPropertyDescriptor(string name, Type propertyType, object component,
			Func<object, object> getter, Action<object, object> setter)
			: base(name, null)
		{
			_propertyType = propertyType;
			_getter = getter;
			_setter = setter;
		}

		public override Type ComponentType => typeof(object);
		public override bool IsReadOnly => false;
		public override Type PropertyType => _propertyType;
		public override bool IsBrowsable => true;

		public override bool CanResetValue(object component) => true;
		public override object GetValue(object component) => _getter(component);
		public override void SetValue(object component, object value) => _setter(component, value);
		public override void ResetValue(object component) => _setter(component, _propertyType == typeof(double) ? (object)double.NaN : 0);
		public override bool ShouldSerializeValue(object component) => true;
	}
}
