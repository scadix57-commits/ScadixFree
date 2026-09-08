namespace Scadix.AxamlDom
{
	/// <summary>
	/// Delegate used for XamlParserSettings.CreateInstanceCallback.
	/// </summary>
	public delegate object CreateInstanceCallback(Type type, object[] arguments);
    /// <summary>
    /// Enum representing the available XAML parsing engines.
    /// </summary>
    public enum XamlParsingEngine
    {
        /// <summary>
        /// Uses the custom reflection-based parser (Manual Engine).
        /// </summary>
        Manual,
        /// <summary>
        /// Uses the native AvaloniaRuntimeXamlLoader (Avalonia Engine).
        /// </summary>
        Avalonia,
        /// <summary>
        /// Tries Avalonia first, falls back to Manual if it fails (Hybrid Engine).
        /// </summary>
        Automatic,
        /// <summary>
        /// Uses the XamlToCSharp source-generator backend.
        /// </summary>
        AXSG
    }
    /// <summary>
    /// Settings used for the XamlParser.
    /// </summary>
    public sealed class XamlParserSettings
	{
		CreateInstanceCallback _createInstanceCallback = Activator.CreateInstance;
		XamlTypeFinder _typeFinder = XamlTypeFinder.CreateWpfTypeFinder();
		IServiceProvider _serviceProvider = DummyServiceProvider.Instance;
		string _currentProjectAssemblyName;




		/// <summary>
		/// Gets/Sets the method used to create object instances.
		/// </summary>
		public CreateInstanceCallback CreateInstanceCallback {
			get { return _createInstanceCallback; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				_createInstanceCallback = value;
			}
		}
        /// <summary>
        /// Gets/Sets the parsing engine to use.
        /// </summary>
        public XamlParsingEngine ParsingEngine { get; set; } = XamlParsingEngine.Automatic;
        /// <summary>
        /// Gets/Sets the type finder to do type lookup.
        /// </summary>
        public XamlTypeFinder TypeFinder {
			get { return _typeFinder; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				_typeFinder = value;
			}
		}
		
		/// <summary>
		/// Gets/Sets the service provider to use to initialize markup extensions.
		/// </summary>
		public IServiceProvider ServiceProvider {
			get { return _serviceProvider; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				_serviceProvider = value;
			}
		}

		/// <summary>
		/// Gets/Sets the Current Projects Assembly Name.
		/// </summary>
		public string CurrentProjectAssemblyName {
			get { return _currentProjectAssemblyName; }
			set {
				_currentProjectAssemblyName = value;
			}
		}

        /// <summary>
        /// Gets/Sets the project's root path on disk.
        /// </summary>
        public string ProjectRootPath { get; set; }

        /// <summary>
        /// Gets/Sets the file path of the XAML being parsed.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets/Sets the project name.
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// Gets/Sets the project's global resources (usually from App.axaml).
        /// </summary>
        public object ProjectResources { get; set; }

        /// <summary>
        /// Gets/Sets the base URI of the document.
        /// </summary>
        public Uri BaseUri { get; set; }


        sealed class DummyServiceProvider : IServiceProvider
		{
			public static readonly DummyServiceProvider Instance = new DummyServiceProvider();
			
			public object GetService(Type serviceType)
			{
				return null;
			}
		}
	}
}
