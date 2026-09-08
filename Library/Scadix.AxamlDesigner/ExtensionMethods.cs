

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Input;
using Avalonia.VisualTree;

namespace Scadix.AxamlDesigner
{
	public static class ExtensionMethods
	{
		public static double Coerce(this double value, double min, double max)
		{
			return Math.Max(Math.Min(value, max), min);
		}

		public static void AddRange<T>(this ICollection<T> col, IEnumerable<T> items)
		{
			foreach (var item in items) {
				col.Add(item);
			}
		}

		/// <summary>
		/// Gets all ancestors in the visual tree (including <paramref name="visual"/> itself).
		/// Returns an empty list if <paramref name="visual"/> is null or not a visual.
		/// </summary>
		public static IEnumerable<Visual> GetVisualAncestors(this Visual visual)
		{
			Visual? current = visual;
			while (current != null) {
				yield return current;
				current = current.GetVisualParent();
			}
		}

		/// <summary>
		/// Gets all ancestors in the visual tree for an AvaloniaObject.
		/// </summary>
		public static IEnumerable<Visual> GetVisualAncestors(this AvaloniaObject obj)
		{
			if (obj is Visual visual)
				return visual.GetVisualAncestors();
			return Enumerable.Empty<Visual>();
		}

        public static void AddCommandHandler(this Control element, System.Windows.Input.ICommand command, Action execute)
        {
            AddCommandHandler(element, command, execute, null);
        }

        public static void AddCommandHandler(this Control element, System.Windows.Input.ICommand command, Action execute, Func<bool>? canExecute)
        {
            var binding = new CommandBinding(
                command,
                (sender, e) => {
                    execute();
                    e.Handled = true;
                },
                canExecute != null ? (sender, e) => {
                    e.CanExecute = canExecute();
                    e.Handled = true;
                }
            : null
            );
            CommandManager.GetCommandBindings(element).Add(binding);
        }
    }
}
