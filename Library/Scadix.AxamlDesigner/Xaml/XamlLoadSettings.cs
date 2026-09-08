 

using System.Reflection;
using Scadix.AxamlDesigner.Services;
using Scadix.AxamlDom;

namespace Scadix.AxamlDesigner.Xaml
{
	/// <summary>
	/// Settings used to load a XAML document.
	/// </summary>
	public sealed class XamlLoadSettings
	{
		public readonly ICollection<Assembly> DesignerAssemblies = new List<Assembly>();
		public readonly List<Action<XamlDesignContext>> CustomServiceRegisterFunctions = new List<Action<XamlDesignContext>>();
		public Action<XamlErrorService> ReportErrors = (errorService) => { };
		XamlTypeFinder typeFinder = XamlTypeFinder.CreateWpfTypeFinder();
		
		public XamlTypeFinder TypeFinder {
			get { return typeFinder; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				typeFinder = value;
			}
		}

		public string CurrentProjectAssemblyName { get; set; }

		public XamlLoadSettings()
		{
			DesignerAssemblies.Add(typeof(XamlDesignContext).Assembly);
		}


        /// <summary>
        /// Gets/Sets the parsing engine to use.
        /// </summary>
        public XamlParsingEngine ParsingEngine { get; set; } = XamlParsingEngine.Avalonia;
        public string FilePath { get; set; }

        public string ProjectRootPath { get; set; }

        /// <summary>
        /// Gets/Sets the base URI of the document.
        /// </summary>
        public Uri BaseUri { get; set; }
    }
}
