 

using System.Reflection;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesign.Services;
using Scadix.AxamlDesigner.OutlineView;
using Scadix.AxamlDesigner.PropertyGrid.Editors;
using Scadix.AxamlDesigner.Services;
using Scadix.AxamlDom;

namespace Scadix.AxamlDesigner.Xaml
{
	/// <summary>
	/// The design context implementation used when editing XAML.
	/// </summary>
	public sealed class XamlDesignContext : DesignContext
	{
		readonly XamlDocument _doc;
		readonly XamlDesignItem _rootItem;
		readonly XamlParserSettings _parserSettings;
		internal readonly XamlComponentService _componentService;
		
		readonly XamlEditOperations _xamlEditOperations;
		
		public XamlEditOperations XamlEditAction {
			get { return _xamlEditOperations; }
		}
		
		internal XamlDocument Document {
			get { return _doc; }
		}
		
		/// <summary>
		/// Gets/Sets the value of the "x:class" property on the root item.
		/// </summary>
		public string ClassName {
			get { return _doc.RootElement.GetXamlAttribute("Class"); }
			//set { _doc.RootElement.SetXamlAttribute("Class", value); }
		}
		
		/// <summary>
		/// Creates a new XamlDesignContext instance.
		/// </summary>
		public XamlDesignContext(XmlReader xamlReader, XamlLoadSettings loadSettings)
		{
			if (xamlReader == null)
				throw new ArgumentNullException("xamlReader");
			if (loadSettings == null)
				throw new ArgumentNullException("loadSettings");
			
			this.Services.AddService(typeof(ISelectionService), new DefaultSelectionService());
			this.Services.AddService(typeof(IComponentPropertyService), new ComponentPropertyService());		
			this.Services.AddService(typeof(IToolService), new DefaultToolService(this));
			this.Services.AddService(typeof(UndoService), new UndoService());
			this.Services.AddService(typeof(ICopyPasteService), new CopyPasteService());
			this.Services.AddService(typeof(IErrorService), new DefaultErrorService(this));
			this.Services.AddService(typeof(IOutlineNodeService), new OutlineNode.OutlineNodeService());
			this.Services.AddService(typeof(IOutlineNodeNameService), new OutlineNodeNameService());
			this.Services.AddService(typeof(ViewService), new DefaultViewService(this));
			this.Services.AddService(typeof(OptionService), new OptionService());

			var xamlErrorService = new XamlErrorService();
			this.Services.AddService(typeof(XamlErrorService), xamlErrorService);
			this.Services.AddService(typeof(IXamlErrorSink), xamlErrorService);
			
			_componentService = new XamlComponentService(this);
			this.Services.AddService(typeof(IComponentService), _componentService);
			
			foreach (Action<XamlDesignContext> action in loadSettings.CustomServiceRegisterFunctions) {
				action(this);
			}
			
			// register default versions of overridable services:
			if (this.Services.GetService(typeof(ITopLevelWindowService)) == null) {
				this.Services.AddService(typeof(ITopLevelWindowService), new WpfTopLevelWindowService());
			}
			
			EditorManager.SetDefaultTextBoxEditorType(typeof(TextBoxEditor));
			EditorManager.SetDefaultComboBoxEditorType(typeof(ComboBoxEditor));
			
			// register extensions from the designer assemblies:
			foreach (Assembly designerAssembly in loadSettings.DesignerAssemblies) {
				this.Services.ExtensionManager.RegisterAssembly(designerAssembly);
				EditorManager.RegisterAssembly(designerAssembly);
			}
			
			_parserSettings = new XamlParserSettings();
			_parserSettings.TypeFinder = loadSettings.TypeFinder;
			_parserSettings.CurrentProjectAssemblyName = loadSettings.CurrentProjectAssemblyName;
			_parserSettings.CreateInstanceCallback = this.Services.ExtensionManager.CreateInstanceWithCustomInstanceFactory;
			_parserSettings.ServiceProvider = this.Services;
            _parserSettings.ParsingEngine = loadSettings.ParsingEngine;
			_parserSettings.FilePath = loadSettings.FilePath;
            _parserSettings.ProjectRootPath = loadSettings.ProjectRootPath;
            _parserSettings.BaseUri = loadSettings.BaseUri;
            _doc = XamlParser.Parse(xamlReader, _parserSettings);
			
			loadSettings.ReportErrors(xamlErrorService);
			
			if (_doc == null) {
				string message;
				if (xamlErrorService != null && xamlErrorService.Errors.Count > 0)
					message = xamlErrorService.Errors[0].Message;
				else
					message = "Could not load document.";
				throw new XamlLoadException(message);
			}

			_rootItem = _componentService.RegisterXamlComponentRecursive(_doc.RootElement);

			if (_rootItem != null) {
				// Support Design.PreviewWith for ResourceDictionary and other non-control roots
				//if (_rootItem.Component is AvaloniaObject ao)
				//{
				//	// Try Avalonia's built-in Design.PreviewWith
				//	var preview = Avalonia.Controls.Design.GetPreviewWith(ao);

				//	// Fallback to Scadix's DesignTimeProperties.PreviewWith
				//	if (preview == null)
				//	{
				//		preview = Scadix.AxamlDom.DesignTimeProperties.GetPreviewWith(ao) as Control;
				//	}

				//	if (preview != null)
				//	{
				//		_rootItem.SetView(preview);
				//	}
				//}

				var rootBehavior = new RootItemBehavior();
				rootBehavior.Intialize(this);
			}

			_xamlEditOperations = new XamlEditOperations(this, _parserSettings);
			
		}


        public XamlDesignContext(Stream xamlReader, XamlLoadSettings loadSettings)
        {
            if (xamlReader == null)
                throw new ArgumentNullException("xamlReader");
            if (loadSettings == null)
                throw new ArgumentNullException("loadSettings");

            this.Services.AddService(typeof(ISelectionService), new DefaultSelectionService());
            this.Services.AddService(typeof(IComponentPropertyService), new ComponentPropertyService());
            this.Services.AddService(typeof(IToolService), new DefaultToolService(this));
            this.Services.AddService(typeof(UndoService), new UndoService());
            this.Services.AddService(typeof(ICopyPasteService), new CopyPasteService());
            this.Services.AddService(typeof(IErrorService), new DefaultErrorService(this));
            this.Services.AddService(typeof(IOutlineNodeService), new OutlineNode.OutlineNodeService());
            this.Services.AddService(typeof(IOutlineNodeNameService), new OutlineNodeNameService());
            this.Services.AddService(typeof(ViewService), new DefaultViewService(this));
            this.Services.AddService(typeof(OptionService), new OptionService());

            var xamlErrorService = new XamlErrorService();
            this.Services.AddService(typeof(XamlErrorService), xamlErrorService);
            this.Services.AddService(typeof(IXamlErrorSink), xamlErrorService);

            _componentService = new XamlComponentService(this);
            this.Services.AddService(typeof(IComponentService), _componentService);

            foreach (Action<XamlDesignContext> action in loadSettings.CustomServiceRegisterFunctions)
            {
                action(this);
            }

            // register default versions of overridable services:
            if (this.Services.GetService(typeof(ITopLevelWindowService)) == null)
            {
                this.Services.AddService(typeof(ITopLevelWindowService), new WpfTopLevelWindowService());
            }

            EditorManager.SetDefaultTextBoxEditorType(typeof(TextBoxEditor));
            EditorManager.SetDefaultComboBoxEditorType(typeof(ComboBoxEditor));

            // register extensions from the designer assemblies:
            foreach (Assembly designerAssembly in loadSettings.DesignerAssemblies)
            {
                this.Services.ExtensionManager.RegisterAssembly(designerAssembly);
                EditorManager.RegisterAssembly(designerAssembly);
            }

            _parserSettings = new XamlParserSettings();
            _parserSettings.TypeFinder = loadSettings.TypeFinder;
            _parserSettings.CurrentProjectAssemblyName = loadSettings.CurrentProjectAssemblyName;
            _parserSettings.CreateInstanceCallback = this.Services.ExtensionManager.CreateInstanceWithCustomInstanceFactory;
            _parserSettings.ServiceProvider = this.Services;
            _parserSettings.ParsingEngine = loadSettings.ParsingEngine;
            _parserSettings.FilePath = loadSettings.FilePath;
            _parserSettings.ProjectRootPath = loadSettings.ProjectRootPath;
            _parserSettings.BaseUri = loadSettings.BaseUri;
            _doc = XamlParser.Parse(xamlReader, _parserSettings);

            loadSettings.ReportErrors(xamlErrorService);

            if (_doc == null)
            {
                string message;
                if (xamlErrorService != null && xamlErrorService.Errors.Count > 0)
                    message = xamlErrorService.Errors[0].Message;
                else
                    message = "Could not load document.";
                throw new XamlLoadException(message);
            }

            _rootItem = _componentService.RegisterXamlComponentRecursive(_doc.RootElement);

            if (_rootItem != null) {


                // Support Design.PreviewWith for ResourceDictionary and other non-control roots
                if (_rootItem.Component is AvaloniaObject ao)
                {
                    // Try Avalonia's built-in Design.PreviewWith
                    var preview = Avalonia.Controls.Design.GetPreviewWith(ao);

                    // Fallback to Scadix's DesignTimeProperties.PreviewWith
                    if (preview == null)
                    {
                        preview = Scadix.AxamlDom.DesignTimeProperties.GetPreviewWith(ao) as Control;
                    }

                    if (preview != null)
                    {
                        _rootItem.SetView(preview);
                    }
                }
                var rootBehavior = new RootItemBehavior();
                rootBehavior.Intialize(this);

				

			}

            _xamlEditOperations = new XamlEditOperations(this, _parserSettings);

        }


        /// <summary>
        /// Saves the XAML DOM into the XML writer.
        /// </summary>
        public override void Save(System.Xml.XmlWriter writer)
		{
			_doc.Save(writer);
		}
		
		/// <summary>
		/// Gets the root item being designed.
		/// </summary>
		public override DesignItem RootItem {
			get { return _rootItem; }
		}
		
		/// <summary>
		/// Gets the parser Settings being used
		/// </summary>
		public XamlParserSettings ParserSettings {
			get { return _parserSettings; }
		}
		
		/// <summary>
		/// Opens a new change group used to batch several changes.
		/// ChangeGroups work as transactions and are used to support the Undo/Redo system.
		/// </summary>
		public override ChangeGroup OpenGroup(string changeGroupTitle, ICollection<DesignItem> affectedItems)
		{
			if (affectedItems == null)
				throw new ArgumentNullException("affectedItems");
			
			UndoService undoService = this.Services.GetRequiredService<UndoService>();
			UndoTransaction g = undoService.StartTransaction(affectedItems);
			g.Title = changeGroupTitle;
			return g;
		}
	}
}
