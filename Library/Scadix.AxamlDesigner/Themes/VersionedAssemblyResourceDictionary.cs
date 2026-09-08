 

using Avalonia.Controls;
using System.ComponentModel;

namespace Scadix.AxamlDesigner.Themes
{
	internal class VersionedAssemblyResourceDictionary : ResourceDictionary, ISupportInitialize
	{
		private static readonly string _uriStart;

		private static readonly int _subLength;

		static VersionedAssemblyResourceDictionary()
		{
			var assemblyName = typeof(VersionedAssemblyResourceDictionary).Assembly.GetName();
			_uriStart = string.Format(@"/{0};v{1};component/", assemblyName.Name, assemblyName.Version);
			_subLength = assemblyName.Name.Length + 1;
		}

		public string RelativePath {get;set;}

		void ISupportInitialize.BeginInit()
		{
			// No-op in Avalonia
		}

		void ISupportInitialize.EndInit()
		{
			// In Avalonia, resource loading is handled differently
		}

		public static string GetXamlNameForType(Type t)
		{
			return _uriStart + t.FullName.Substring(_subLength).Replace(".","/").ToLower() + ".axaml";
		}
	}
}
