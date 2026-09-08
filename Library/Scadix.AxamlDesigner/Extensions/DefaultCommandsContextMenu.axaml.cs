using Avalonia.Controls;
using Avalonia.Labs.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class DefaultCommandsContextMenu : ContextMenu
	{
		private DesignItem? _designItem;

		public DefaultCommandsContextMenu()
		{
			InitializeComponent();
		}

		public DefaultCommandsContextMenu(DesignItem designItem)
		{
			_designItem = designItem;
			InitializeComponent();
			BuildItems();
		}

		private DesignSurface? GetDesignSurface()
		{
			if (_designItem?.Services?.DesignPanel is DesignPanel panel)
				return panel.DesignSurface;
			return null;
		}

		private void BuildItems()
		{
			Items.Clear();
			Items.Add(MakeItem("Cut",    "Ctrl+X", "CutIcon",    () => GetDesignSurface()?.Cut(),    () => GetDesignSurface()?.CanCut()    == true));
			Items.Add(MakeItem("Copy",   "Ctrl+C", "CopyIcon",   () => GetDesignSurface()?.Copy(),   () => GetDesignSurface()?.CanCopy()   == true));
			Items.Add(MakeItem("Paste",  "Ctrl+V", "PasteIcon",  () => GetDesignSurface()?.Paste(),  () => GetDesignSurface()?.CanPaste()  == true));
			Items.Add(new Separator());
			Items.Add(MakeItem("Delete", "Delete", "DeleteIcon", () => GetDesignSurface()?.Delete(), () => GetDesignSurface()?.CanDelete() == true));
			Items.Add(new Separator());
			Items.Add(MakeItem("Undo",   "Ctrl+Z", "UndoIcon",   () => GetDesignSurface()?.Undo(),   () => GetDesignSurface()?.CanUndo()   == true));
			Items.Add(MakeItem("Redo",   "Ctrl+Y", "RedoIcon",   () => GetDesignSurface()?.Redo(),   () => GetDesignSurface()?.CanRedo()   == true));
		}

		private static MenuItem MakeItem(string header, string gesture, string iconName, Action execute, Func<bool> canExecute)
		{
			return new MenuItem
			{
				Header       = header,
				InputGesture = Avalonia.Input.KeyGesture.Parse(gesture),
				Command      = new DelegateCommand(execute, canExecute),
				Icon         = MakeIcon(iconName),
			};
		}

		private static Image? MakeIcon(string iconName)
		{
			try
			{
				var uri = new Uri($"avares://Scadix.AxamlDesigner/Images/Icons.16x16.{iconName}.png");
				return new Image
				{
					Source = new Bitmap(AssetLoader.Open(uri)),
					Width  = 16,
					Height = 16,
				};
			}
			catch { return null; }
		}

		/// <summary>
		/// Plain ICommand — no routing, no visual tree dependency.
		/// Execute and CanExecute call the delegates directly on DesignSurface.
		/// CanExecuteChanged is wired to RequerySuggested so the UI refreshes automatically.
		/// </summary>
		private sealed class DelegateCommand : System.Windows.Input.ICommand
		{
			private readonly Action _execute;
			private readonly Func<bool> _canExecute;

			public DelegateCommand(Action execute, Func<bool> canExecute)
			{
				_execute    = execute;
				_canExecute = canExecute;
			}

			public event EventHandler? CanExecuteChanged
			{
				add    => CommandManager.RequerySuggested += value;
				remove => CommandManager.RequerySuggested -= value;
			}

			public bool CanExecute(object? parameter) => _canExecute();
			public void Execute(object? parameter)    => _execute();
		}
	}
}
