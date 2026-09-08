 

using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner
{
	static class SharedInstances
	{
		public static readonly bool BoxedTrue = true;
        public static readonly bool BoxedFalse = false;
		internal static readonly double BoxedDouble1 = 1.0;
		internal static readonly double BoxedDouble0 = 0.0;
		internal static readonly object[] EmptyObjectArray = new object[0];
		internal static readonly DesignItem[] EmptyDesignItemArray = new DesignItem[0];
		
		internal static object Box(bool value)
		{
			return value ? BoxedTrue : BoxedFalse;
		}
	}

	static class SharedInstances<T> where T: struct, IConvertible
	{
		private static Dictionary<T, object> _boxedEnumValues;

		static SharedInstances()
		{
			_boxedEnumValues = new Dictionary<T, object>();
			foreach (var value in Enum.GetValues(typeof(T)))
			{
				_boxedEnumValues.Add((T)value, value);
			}
		}

		internal static object Box(T value)
		{
			return _boxedEnumValues[value];
		}
	}
}
