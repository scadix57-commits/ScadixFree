
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Labs.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Services;
using Scadix.AxamlDesigner.Xaml;

namespace Scadix.AxamlDesigner
{
	/// <summary>
	/// Surface hosting the WPF designer.
	/// </summary>
	[TemplatePart(Name = "PART_DesignContent", Type = typeof(GridLinesControl))]
	[TemplatePart(Name = "PART_Zoom", Type = typeof(ZoomControl))]
	public partial class DesignSurface : GridLinesControl, INotifyPropertyChanged
	{
		private FocusNavigator _focusNav;
		
		protected override Type StyleKeyOverride => typeof(DesignSurface);
		
		public DesignSurface()
		{
			//Propertygrid should show no inherited Datacontext!
			this.DataContext = null;

			//this.AddCommandHandler(ApplicationCommands.Undo, Undo, CanUndo);
			//this.AddCommandHandler(ApplicationCommands.Redo, Redo, CanRedo);
			//this.AddCommandHandler(ApplicationCommands.Copy, Copy, CanCopy);
			//this.AddCommandHandler(ApplicationCommands.Cut, Cut, CanCut);
			//this.AddCommandHandler(ApplicationCommands.Delete, Delete, CanDelete);
			//this.AddCommandHandler(ApplicationCommands.Paste, Paste, CanPaste);
			//this.AddCommandHandler(ApplicationCommands.SelectAll, SelectAll, CanSelectAll);
			
			//this.AddCommandHandler(Commands.AlignTopCommand, () => ModelTools.ArrangeItems(this.DesignContext.Services.Selection.SelectedItems, ArrangeDirection.Top), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);
			//this.AddCommandHandler(Commands.AlignMiddleCommand, () => ModelTools.ArrangeItems(this.DesignContext.Services.Selection.SelectedItems, ArrangeDirection.VerticalMiddle), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);
			//this.AddCommandHandler(Commands.AlignBottomCommand, () => ModelTools.ArrangeItems(this.DesignContext.Services.Selection.SelectedItems, ArrangeDirection.Bottom), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);
			//this.AddCommandHandler(Commands.AlignLeftCommand, () => ModelTools.ArrangeItems(this.DesignContext.Services.Selection.SelectedItems, ArrangeDirection.Left), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);
			//this.AddCommandHandler(Commands.AlignCenterCommand, () => ModelTools.ArrangeItems(this.DesignContext.Services.Selection.SelectedItems, ArrangeDirection.HorizontalMiddle), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);
			//this.AddCommandHandler(Commands.AlignRightCommand, () => ModelTools.ArrangeItems(this.DesignContext.Services.Selection.SelectedItems, ArrangeDirection.Right), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);
			
			//this.AddCommandHandler(Commands.RotateLeftCommand, () => ModelTools.ApplyTransform(this.DesignContext.Services.Selection.PrimarySelection, new RotateTransform(-90), true, RenderTransformProperty), () => this.DesignContext.Services.Selection.PrimarySelection != null);
			//this.AddCommandHandler(Commands.RotateRightCommand, () => ModelTools.ApplyTransform(this.DesignContext.Services.Selection.PrimarySelection, new RotateTransform(90), true, RenderTransformProperty), () => this.DesignContext.Services.Selection.PrimarySelection != null);

			//this.AddCommandHandler(Commands.StretchToSameWidthCommand, () => ModelTools.StretchItems(this.DesignContext.Services.Selection.SelectedItems, StretchDirection.Width), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);
			//this.AddCommandHandler(Commands.StretchToSameHeightCommand, () => ModelTools.StretchItems(this.DesignContext.Services.Selection.SelectedItems, StretchDirection.Height), () => this.DesignContext.Services.Selection.SelectedItems.Count() > 1);

            _sceneContainer = new Border();
            _sceneContainer.UseLayoutRounding = true;
            _sceneContainer.Margin = new Thickness(20);

            DragDrop.SetAllowDrop(_sceneContainer, false);

            _designPanel = new DesignPanel() {Child = _sceneContainer, DesignSurface = this};

			 

        }

		internal DesignPanel _designPanel;
		private ContentControl _partDesignContent;
		private Border _sceneContainer;

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_partDesignContent = e.NameScope.Find("PART_DesignContent") as GridLinesControl;
			if (_partDesignContent != null)
				_partDesignContent.Content = _designPanel;

			this.ZoomControl = e.NameScope.Find("PART_Zoom") as ZoomControl;
			
			OnPropertyChanged("ZoomControl");
		}
		
		private bool enableBringIntoView = false;
		
		public void ScrollIntoView(DesignItem designItem)
		{
			// In Avalonia, BringIntoView is called directly on the control
			(designItem.View as Control)?.BringIntoView();
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if (ZoomControl != null && e.Source == ZoomControl)
			{
				UnselectAll();
			}
			if (DesignPanel != null)
				DesignPanel.Focus();
		}

		public ZoomControl ZoomControl { get; private set; }
		
		DesignContext _designContext;

		/// <summary>
		/// Gets the active design context.
		/// </summary>
		public DesignContext DesignContext {
			get { return _designContext; }
		}
		
		/// <summary>
		/// Gets the DesignPanel
		/// </summary>
		public DesignPanel DesignPanel {
			get { return _designPanel; }
		}
		
		/// <summary>
		/// Initializes the designer content from the specified XmlReader.
		/// </summary>
		public void LoadDesigner(XmlReader xamlReader, XamlLoadSettings loadSettings)
		{
			UnloadDesigner();
			loadSettings = loadSettings ?? new XamlLoadSettings();
			loadSettings.CustomServiceRegisterFunctions.Add(
				context => context.Services.AddService(typeof(IDesignPanel), _designPanel));
			InitializeDesigner(new XamlDesignContext(xamlReader, loadSettings));
		}

		/// <summary>
		/// Initializes the designer content from a Stream.
		/// Using a Stream lets XmlDocument.Load() preserve literal newlines inside
		/// attribute values (e.g. multi-line Path Data="...") that XmlReader would
		/// otherwise collapse to spaces via attribute-value normalisation.
		/// </summary>
		public void LoadDesigner(Stream xamlStream, XamlLoadSettings loadSettings)
		{
			UnloadDesigner();
			loadSettings = loadSettings ?? new XamlLoadSettings();
			loadSettings.CustomServiceRegisterFunctions.Add(
				context => context.Services.AddService(typeof(IDesignPanel), _designPanel));
			InitializeDesigner(new XamlDesignContext(xamlStream, loadSettings));
		}
		
		/// <summary>
		/// Saves the designer content into the specified XmlWriter.
		/// </summary>
		public void SaveDesigner(XmlWriter writer)
		{
			_designContext?.Save(writer);
		}
		
		void InitializeDesigner(DesignContext context)
		{
			_designContext = context;
			_designPanel.Context = context;
			_designPanel.ClearContextMenu();

			if (context.RootItem != null) {
				_sceneContainer.Child = context.RootItem.View;
			}
			
			context.Services.RunWhenAvailable<UndoService>(
				undoService => undoService.UndoStackChanged += delegate {
					Avalonia.Labs.Input.CommandManager.InvalidateRequerySuggested();
				}
			);
			context.Services.Selection.SelectionChanged += delegate {
				Avalonia.Labs.Input.CommandManager.InvalidateRequerySuggested();
			};
			
			context.Services.AddService(typeof(IKeyBindingService), new DesignerKeyBindings(this));
			_focusNav=new FocusNavigator(this);
			_focusNav.Start();
			
			OnPropertyChanged("DesignContext");
		}
		
		/// <summary>
		/// Unloads the designer content.
		/// </summary>
		public void UnloadDesigner()
		{
			if (_designContext != null) {
				foreach (object o in _designContext.Services.AllServices) {
					IDisposable d = o as IDisposable;
					if (d != null) d.Dispose();
				}
			}
			_designContext = null;
			_designPanel.Context = null;
			_sceneContainer.Child = null;
			_designPanel.Adorners.Clear();
		}

        #region Commands
        //private void InitializeKeyBindings()
        //{
        //    this.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.X, KeyModifiers.Control), Command = CutCommand });
        //    this.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.C, KeyModifiers.Control), Command = CopyCommand });
        //    this.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, KeyModifiers.Control), Command = UndoCommand });
        //    this.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.V, KeyModifiers.Control), Command = PasteCommand });
        //    this.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Delete), Command = DeleteCommand });
        //    this.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Y, KeyModifiers.Control), Command = RedoCommand });
        //}
        public bool CanUndo()
		{
			UndoService undoService = GetService<UndoService>();
			return undoService != null && undoService.CanUndo;
		}
        
        public void Undo()
		{
			UndoService undoService = GetService<UndoService>();
			IUndoAction action = undoService.UndoActions.First();
			Debug.WriteLine("Undo " + action.Title);
			undoService.Undo();
			_designContext.Services.Selection.SetSelectedComponents(GetLiveElements(action.AffectedElements));
		}

		public bool CanRedo()
		{
			UndoService undoService = GetService<UndoService>();
			return undoService != null && undoService.CanRedo;
		}
        
        public void Redo()
		{
			UndoService undoService = GetService<UndoService>();
			IUndoAction action = undoService.RedoActions.First();
			Debug.WriteLine("Redo " + action.Title);
			undoService.Redo();
			_designContext.Services.Selection.SetSelectedComponents(GetLiveElements(action.AffectedElements));
		}

		public bool CanCopy()
		{
			return _designContext?.Services?.CopyPasteService?.CanCopy(_designContext) == true;
		}
        
        public void Copy()
		{
			_designContext?.Services?.CopyPasteService?.Copy(_designContext);
		}

		public bool CanCut()
		{
			return _designContext?.Services?.CopyPasteService?.CanCut(_designContext) == true;
		}
       
        public void Cut()
		{
			_designContext?.Services?.CopyPasteService?.Cut(_designContext);
		}

		public bool CanDelete()
		{
			return _designContext?.Services?.CopyPasteService?.CanDelete(_designContext) == true;
		}
      
        public void Delete()
		{
			_designContext?.Services?.CopyPasteService?.Delete(_designContext);
		}

		public bool CanPaste()
		{
			return _designContext?.Services?.CopyPasteService?.CanPaste(_designContext) == true;
		}
        [RelayCommand(CanExecute = nameof(CanPaste))]
        public void Paste()
		{
			_designContext?.Services?.CopyPasteService?.Paste(_designContext);
		}

		public bool CanSelectAll()
		{
			return DesignContext != null;
		}
        
        public void SelectAll()
		{
			var items = Descendants(DesignContext.RootItem).Where(item => ModelTools.CanSelectComponent(item)).ToArray();
			DesignContext.Services.Selection.SetSelectedComponents(items);
		}
     
        public void UnselectAll()
		{
			DesignContext.Services.Selection.SetSelectedComponents(null);
		}

		//TODO: Share with Outline / PlacementBehavior
		public static IEnumerable<DesignItem> DescendantsAndSelf(DesignItem item)
		{
			yield return item;
			foreach (var child in Descendants(item)) {
				yield return child;
			}
		}

		public static IEnumerable<DesignItem> Descendants(DesignItem item)
		{
			if (item.ContentPropertyName != null) {
				var content = item.ContentProperty;
				if (content.IsCollection) {
					foreach (var child in content.CollectionElements) {
						foreach (var child2 in DescendantsAndSelf(child)) {
							yield return child2;
						}
					}
				} else {
					if (content.Value != null) {
						foreach (var child2 in DescendantsAndSelf(content.Value)) {
							yield return child2;
						}
					}
				}
			}
		}

		// Filters an element list, dropping all elements that are not part of the xaml document
		// (e.g. because they were deleted).
		static List<DesignItem> GetLiveElements(ICollection<DesignItem> items)
		{
			List<DesignItem> result = new List<DesignItem>(items.Count);
			foreach (DesignItem item in items) {
				if (ModelTools.IsInDocument(item) && ModelTools.CanSelectComponent(item)) {
					result.Add(item);
				}
			}
			return result;
		}

		T GetService<T>() where T : class
		{
			if (_designContext != null)
				return _designContext.Services.GetService<T>();
			else
				return null;
		}

		#endregion

		#region INotifyPropertyChanged implementation

		public event PropertyChangedEventHandler PropertyChanged;
		
		public void OnPropertyChanged(string propertyName)
		{
			var ev = PropertyChanged;
			if (ev != null)
				ev(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
	}
}
