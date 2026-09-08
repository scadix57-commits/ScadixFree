using AvaloniaEdit.Editing;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Interfaces;
using Scadix.AxamlDesigner;
using Scadix.AxamlDesigner.Services;
using Scadix.AxamlDesigner.Xaml;
using Scadix.AxamlDom;
using System;
using System.Collections.Generic;

namespace Scadix.Designer
{
	public partial class Document : INotifyPropertyChanged
	{
		public Document(string tempName, string text)
		{
			this.tempName = tempName;
			this.text = text;
			IsDirty = false;
		}

		public Document(string filePath)
		{
			this.filePath = filePath;
			this.text = string.Empty;
			ReloadFile();
		}

		public bool IsXamlFile =>
			FilePath == null ||   // new unsaved documents default to XAML mode
			FilePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
			FilePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || FilePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
		
		string tempName;
		DesignSurface designSurface = new DesignSurface();

		string text;

		public string Text {
			get {
				return text;
			}
			set {
				if (text != value) {
					text = value;
					IsDirty = true;
					RaisePropertyChanged("Text");
				}
			}
		}

		DocumentMode mode;

		public DocumentMode Mode {
			get {
				return mode;
			}
			set {
				if (mode == value) return;
				mode = value;
				if (IsXamlFile)
				{
					if (InDesignMode)
						UpdateDesign();
					else
					{
						UpdateXaml();
						if (DesignContext?.Services?.Selection?.PrimarySelection != null)
						{
							var sel = DesignContext.Services.Selection.PrimarySelection;
							var ln = ((PositionXmlElement)((XamlDesignItem)sel).XamlObject.XmlElement).LineNumber;
						}
					}
				}
				// Non-XAML files: mode is always Xaml — nothing to update
				RaisePropertyChanged("Mode");
				RaisePropertyChanged("InXamlMode");
				RaisePropertyChanged("InDesignMode");
			}
		}

		public bool InXamlMode {
			get { return Mode == DocumentMode.Xaml; }
		}

		public bool InDesignMode {
			get { return Mode == DocumentMode.Design; }
		}

		string? filePath;

		public string? FilePath {
			get {
				return filePath;
			}
			private set {
				filePath = value;
				RaisePropertyChanged("FilePath");
				RaisePropertyChanged("FileName");
				RaisePropertyChanged("Title");
				RaisePropertyChanged("Name");
			}
		}

		bool isDirty;

		public bool IsDirty {
			get {
				return isDirty;
			}
			private set {
				isDirty = value;
				RaisePropertyChanged("IsDirty");
				RaisePropertyChanged("Name");
				RaisePropertyChanged("Title");
			}
		}

		public XamlElementLineInfo? xamlElementLineInfo;
		public XamlElementLineInfo? XamlElementLineInfo
		{
			get
			{
				return xamlElementLineInfo;
			}
			private set
			{
				xamlElementLineInfo = value;
				RaisePropertyChanged("XamlElementLineInfo");
			}
		}

		public string? FileName {
			get {
				if (FilePath == null) return null;
				return Path.GetFileName(FilePath);
			}
		}

		public string Name {
			get {
				return FileName ?? tempName;
			}
		}

		public string Title {
			get {
				return IsDirty ? Name + "*" : Name;
			}
		}

		public DesignSurface DesignSurface {
			get { return designSurface; }
		}

		public DesignContext DesignContext {
			get { return designSurface.DesignContext; }
		}

		public UndoService? UndoService {
			get { return DesignContext.Services.GetService<UndoService>(); }
		}

		public ISelectionService? SelectionService {
			get {
				if (InDesignMode) {
					return DesignContext.Services.Selection;
				}
				return null;
			}
		}

		public XamlErrorService? XamlErrorService {
			get {
				if (DesignContext != null) {
					return DesignContext.Services.GetService<XamlErrorService>();
				}
				return null;
			}
		}

		IOutlineNode? outlineRoot;

		public IOutlineNode? OutlineRoot {
			get {
				return outlineRoot;
			}
			private set {
				outlineRoot = value;
				RaisePropertyChanged("OutlineRoot");
			}
		}

		void ReloadFile()
		{
            if (FilePath != null)
            {
			    Text = File.ReadAllText(FilePath);
			    // Reloading text — designer will be updated when Mode is set or refreshed
			    /* if (IsXamlFile)
			        UpdateDesign(); */
			    IsDirty = false;
            }
		}

		public void Save()
		{
			// Only serialize from designer for XAML files in Design mode
			if (IsXamlFile && InDesignMode)
				UpdateXaml();

            if (FilePath != null)
            {
			    File.WriteAllText(FilePath, Text);
			    IsDirty = false;
            }
		}

		public void SaveAs(string filePath)
		{
			FilePath = filePath;
			Save();
		}

		public void Refresh()
		{
			if (!IsXamlFile) return;
			UpdateXaml();
			UpdateDesign();
		}

		void UpdateXaml()
		{
			var sb = new StringBuilder();
			using (var xmlWriter = new XamlXmlWriter(sb)) {
				DesignSurface.SaveDesigner(xmlWriter);
				Dictionary<XamlElementLineInfo, XamlElementLineInfo> d;
				Text = XamlFormatter.Format(sb.ToString(), out d);

				if (DesignSurface.DesignContext.Services.Selection.PrimarySelection != null)
				{
					var item = DesignSurface.DesignContext.Services.Selection.PrimarySelection;
					var line = ((PositionXmlElement) ((XamlDesignItem) item).XamlObject.XmlElement).LineNumber;
					var pos = (((XamlDesignItem)item).XamlObject.PositionXmlElement).LinePosition;
					    var newP = d.FirstOrDefault(x => x.Key.LineNumber == line && x.Key.LinePosition == pos);
					    XamlElementLineInfo = newP.Value;
                    }
				}
			}

		void UpdateDesign()
		{
			OutlineRoot = null;
			// Pass a MemoryStream directly to LoadDesigner(Stream) so that
			// XmlDocument.Load(Stream) is used internally — this preserves literal
			// \r\n inside attribute values (e.g. multi-line Path Data="...") that
			// XmlReader attribute-value normalisation would otherwise collapse to spaces.
			var bytes = System.Text.Encoding.UTF8.GetBytes(Text);
			using (var stream = new MemoryStream(bytes)) {
				XamlLoadSettings settings = new XamlLoadSettings();
                settings.ParsingEngine = Settings.Default.SelectedParsingEngine;
                settings.CurrentProjectAssemblyName = DesignerProjectContext.CurrentProjectName;
                settings.ProjectRootPath = Settings.Default.ProjectPath;
                foreach (var assNode in Toolbox.Instance.AssemblyNodes)
				{
					settings.DesignerAssemblies.Add(assNode.Assembly);
                }
				settings.TypeFinder = MyTypeFinder.Instance;
                if (!string.IsNullOrEmpty(FilePath) && !string.IsNullOrEmpty(settings.CurrentProjectAssemblyName))
                {
                    var projectRoot = settings.ProjectRootPath ?? "";
                    var relativePath = FilePath.Replace(projectRoot, "").Replace("\\", "/").TrimStart('/');
                    try
                    {
                        settings.BaseUri = new Uri($"avares://{settings.CurrentProjectAssemblyName}/{relativePath}");
                    }
                    catch { /* Ignore URI creation errors */ }
                }
                if (!string.IsNullOrEmpty(FilePath))
				{
					settings.CurrentProjectAssemblyName = SolutionService.GetProjectNameFromFilePath(FilePath);
					settings.FilePath = FilePath;
                }
                if (!string.IsNullOrEmpty(settings.CurrentProjectAssemblyName))
                {
                    EnsureProjectAssemblyRegistered(settings.CurrentProjectAssemblyName, FilePath);
                }

                settings.CustomServiceRegisterFunctions.Add(
                    context => {
                        context.Services.AddService(typeof(IEventHandlerService), new Services.EventHandlerService());
                        context.Services.AddService(typeof(XamlLoadSettings), settings);
                    });

				DesignSurface.LoadDesigner(stream, settings);
			}
			if (DesignContext.RootItem != null) {
				OutlineRoot = DesignContext.RootItem.CreateOutlineNode();
				UndoService.UndoStackChanged += new EventHandler(UndoService_UndoStackChanged);
			}
			RaisePropertyChanged("SelectionService");
			RaisePropertyChanged("XamlErrorService");
		}

        /// <summary>
        /// Ensures the project assembly is registered in MyTypeFinder before XAML parsing begins.
        /// Also pre-registers every "using:Namespace" found in the XAML text so that
        /// ParseNamespace() can map them to the project assembly immediately.
        /// </summary>
        static void EnsureProjectAssemblyRegistered(string projectAssemblyName, string? filePath)
        {
            try
            {
                // 1. Find / load the project assembly
                System.Reflection.Assembly? projectAsm = MyTypeFinder.Instance.RegisteredAssemblies
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, projectAssemblyName, StringComparison.OrdinalIgnoreCase));

                if (projectAsm == null)
                {
                    // Try bin folder first
                    var projectDir = !string.IsNullOrEmpty(filePath)
                        ? SolutionService.GetProjectDirectoryFromFilePath(filePath)
                        : null;

                    if (!string.IsNullOrEmpty(projectDir))
                    {
                        var binFolder = System.IO.Path.Combine(projectDir, "bin");
                        if (System.IO.Directory.Exists(binFolder))
                        {
                            var dll = System.IO.Directory.GetFiles(binFolder, $"{projectAssemblyName}.dll", System.IO.SearchOption.AllDirectories)
                                .Where(f => !f.Contains("\\ref\\") && !f.Contains("\\runtimes\\"))
                                .OrderByDescending(System.IO.File.GetLastWriteTime)
                                .FirstOrDefault();

                            if (dll != null)
                                projectAsm = System.Reflection.Assembly.LoadFrom(dll);
                        }
                    }

                    // Fallback: AppDomain
                    if (projectAsm == null)
                    {
                        projectAsm = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => !a.IsDynamic &&
                                string.Equals(a.GetName().Name, projectAssemblyName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (projectAsm != null)
                        MyTypeFinder.Instance.RegisterAssembly(projectAsm);
                }

                if (projectAsm == null) return;

                // 2. Pre-register every "using:Namespace" found in the XAML so that
                //    XamlTypeFinder.ParseNamespace() finds the assembly in _registeredAssemblies.
                //    We read the root element's xmlns attributes directly.
                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    MyTypeFinder.Instance.PreRegisterXamlNamespaces(filePath, projectAsm);
                }

                MyTypeFinder.Instance.SetProjectAssembly(projectAsm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Designer] EnsureProjectAssemblyRegistered failed for '{projectAssemblyName}': {ex.Message}");
            }
        }

		void UndoService_UndoStackChanged(object? sender, EventArgs e)
		{
			IsDirty = true;
			if (InXamlMode) {
				UpdateXaml();
			}
		}
        #region Command
        [RelayCommand]
        private void CopyMouse(TextArea textArea)
        {
            ApplicationCommands.Copy.Execute(null, textArea);
        }

        [RelayCommand]
        private void CutMouse(TextArea textArea)
        {
            ApplicationCommands.Cut.Execute(null, textArea);
        }

        [RelayCommand]
        private void PasteMouse(TextArea textArea)
        {
            ApplicationCommands.Paste.Execute(null, textArea);
        }

        [RelayCommand]
        private void SelectAllMouse(TextArea textArea)
        {
            ApplicationCommands.SelectAll.Execute(null, textArea);
        }

        [RelayCommand]
        private void UndoMouse(TextArea textArea)
        {
            ApplicationCommands.Undo.Execute(null, textArea);
        }
        #endregion
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler? PropertyChanged;

		void RaisePropertyChanged(string name)
		{
			if (PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(name));
			}
		}
        /// <summary>
        /// Replaces literal \r\n (and \n) that appear inside XML attribute values with
        /// the character reference &#xA; so that XmlReader does not normalise them away.
        /// Characters outside attribute values (element content, comments, etc.) are left
        /// unchanged.
        /// </summary>
        static string PreserveAttributeNewlines(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return xml;
            var result = new System.Text.StringBuilder(xml.Length);
            bool inAttr = false;
            char attrQuote = '"';
            for (int i = 0; i < xml.Length; i++)
            {
                char c = xml[i];
                if (!inAttr)
                {
                    if (c == '"' || c == '\'')
                    {
                        // Only enter attribute-value mode when preceded by '='
                        // (skip quote chars that appear in element content)
                        int j = i - 1;
                        while (j >= 0 && xml[j] == ' ') j--;
                        if (j >= 0 && xml[j] == '=')
                        {
                            inAttr = true;
                            attrQuote = c;
                        }
                    }
                    result.Append(c);
                }
                else
                {
                    if (c == attrQuote)
                    {
                        inAttr = false;
                        result.Append(c);
                    }
                    else if (c == '\r')
                    {
                        result.Append("&#xA;");
                        // skip the \n that follows \r\n
                        if (i + 1 < xml.Length && xml[i + 1] == '\n')
                            i++;
                    }
                    else if (c == '\n')
                    {
                        result.Append("&#xA;");
                    }
                    else
                    {
                        result.Append(c);
                    }
                }
            }
            return result.ToString();
        }
        #endregion
    }

	public enum DocumentMode
	{
		Xaml, Design
	}
}
