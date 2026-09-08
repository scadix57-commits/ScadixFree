 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Input;
using Scadix.AxamlDesign.Interfaces;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.OutlineView
{
	public partial class Outline :UserControl
	{
		public Outline()
		{
			InitializeComponent();

			this.AddCommandHandler(ApplicationCommands.Undo,
				() => ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.Undo(),
				() => Root == null ? false : ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.CanUndo());
			this.AddCommandHandler(ApplicationCommands.Redo,
				() => ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.Redo(),
				() => Root == null ? false : ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.CanRedo());
			this.AddCommandHandler(ApplicationCommands.Copy,
				() => ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.Copy(),
				() => Root == null ? false : ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.CanCopy());
			this.AddCommandHandler(ApplicationCommands.Cut,
				() => ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.Cut(),
				() => Root == null ? false : ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.CanCut());
			this.AddCommandHandler(ApplicationCommands.Delete,
				() => ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.Delete(),
				() => Root == null ? false : ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.CanDelete());
			this.AddCommandHandler(ApplicationCommands.Paste,
				() => ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.Paste(),
				() => Root == null ? false : ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.CanPaste());
			this.AddCommandHandler(ApplicationCommands.SelectAll,
				() => ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.SelectAll(),
				() => Root == null ? false : ((DesignPanel) Root.DesignItem.Services.DesignPanel).DesignSurface.CanSelectAll());
		}

		public static readonly StyledProperty<IOutlineNode> RootProperty =
			AvaloniaProperty.Register<Outline, IOutlineNode>("Root");

		public IOutlineNode Root
		{
			get { return GetValue(RootProperty); }
			set { SetValue(RootProperty, value); }
		}
		
		public object OutlineContent {
			get { return this; }
		}
	}
}
