using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Scadix.AxamlDesigner.OutlineView;
using Scadix.AxamlDesigner.Services;

namespace Scadix.Designer
{
	public partial class ToolboxView : UserControl
    {
        private PointerEventArgs? _lastPointerEvent;
        public ToolboxView()
		{
			DataContext = Toolbox.Instance;
            InitializeComponent();

            // Assuming DragListener has been migrated to use Pointer events
           
		 	uxTreeView.SelectionChanged += uxTreeView_SelectionChanged;
            uxTreeView.PointerMoved += TreeView_PointerMoved;
            new DragListener(this).DragStarted += Toolbox_DragStarted;
        }

		private void uxTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			PrepareTool(uxTreeView.SelectedItem as ControlNode, false);
		}

		private void Toolbox_DragStarted(object? sender, PointerEventArgs e)
		{
            PrepareTool(e.GetDataContext() as ControlNode, true);
			}
        private void TreeView_PointerMoved(object? sender, PointerEventArgs e)
        {
            _lastPointerEvent = e;
        }
        private async void PrepareTool(ControlNode? node, bool drag, PointerPressedEventArgs? e = null)
		{
            if (node != null)
            {
                var tool = new CreateComponentTool(node.Type);
                if (MainWindowViewModel.Instance.CurrentDocument != null)
                {
                    MainWindowViewModel.Instance.CurrentDocument.DesignContext.Services.Tool.CurrentTool = tool;
                    if (drag)
                    {
                        // Create data object for drag and drop
                        var dataObject = new DataObject();
                        dataObject.Set(typeof(CreateComponentTool).FullName!, tool);
                        DragDrop.DoDragDrop(_lastPointerEvent, dataObject, DragDropEffects.Copy);
                    }
                }
            }
        }

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Delete) {
				Remove();
			}
            base.OnKeyDown(e);
		}

		private void Remove()
		{
			AssemblyNode? node = uxTreeView.SelectedItem as AssemblyNode;
			if (node != null) {
				Toolbox.Instance.Remove(node);
			}
		}
		
		private async void BrowseForAssemblies_OnClick(object? sender, RoutedEventArgs e)
		{
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Open Assemblies",
                    FileTypeFilter = new[] { new FilePickerFileType("Assemblies") { Patterns = new[] { "*.dll" } } },
                    AllowMultiple = true
                });

                foreach (var file in files)
                {
                    Toolbox.Instance.AddAssembly(file.Path.LocalPath);
                }
            }
		}
	}
}
