

using Avalonia.Labs.Input;

namespace Scadix.AxamlDesigner
{
	/// <summary>
	/// Standard application commands (Avalonia equivalent of WPF ApplicationCommands).
	/// </summary>
	public static class ApplicationCommands
	{
		public static readonly RoutedCommand Undo = new RoutedCommand(nameof(Undo));
		public static readonly RoutedCommand Redo = new RoutedCommand(nameof(Redo));
		public static readonly RoutedCommand Copy = new RoutedCommand(nameof(Copy));
		public static readonly RoutedCommand Cut = new RoutedCommand(nameof(Cut));
		public static readonly RoutedCommand Paste = new RoutedCommand(nameof(Paste));
		public static readonly RoutedCommand Delete = new RoutedCommand(nameof(Delete));
		public static readonly RoutedCommand SelectAll = new RoutedCommand(nameof(SelectAll));
	}

	/// <summary>
	/// Designer-specific commands.
	/// </summary>
	public static class Commands
	{
		public static readonly RoutedCommand AlignTopCommand = new RoutedCommand(nameof(AlignTopCommand));
        public static readonly RoutedCommand AlignMiddleCommand = new RoutedCommand(nameof(AlignMiddleCommand));
        public static readonly RoutedCommand AlignBottomCommand = new RoutedCommand(nameof(AlignBottomCommand));
        public static readonly RoutedCommand AlignLeftCommand = new RoutedCommand(nameof(AlignLeftCommand));
        public static readonly RoutedCommand AlignCenterCommand = new RoutedCommand(nameof(AlignCenterCommand));
        public static readonly RoutedCommand AlignRightCommand = new RoutedCommand(nameof(AlignRightCommand));
        public static readonly RoutedCommand RotateLeftCommand = new RoutedCommand(nameof(RotateLeftCommand));
        public static readonly RoutedCommand RotateRightCommand = new RoutedCommand(nameof(RotateRightCommand));
		public static readonly RoutedCommand StretchToSameWidthCommand = new RoutedCommand(nameof(StretchToSameWidthCommand));
		public static readonly RoutedCommand StretchToSameHeightCommand = new RoutedCommand(nameof(StretchToSameHeightCommand));
	}
}
