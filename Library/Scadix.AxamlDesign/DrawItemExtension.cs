

using Avalonia.Input;

namespace Scadix.AxamlDesign
{
	/// <summary>
	/// Behavior interface implemented by container elements to support resizing
	/// drawing new Elements
	/// </summary>
	public interface IDrawItemExtension
	{
		/// <summary>
		/// Returns if the specified type can be drawn.
		/// </summary>
		/// <param name="createItemType">The type to check.</param>
		/// <returns>True if the specified type can be drawn, otherwise false.</returns>
		bool CanItemBeDrawn(Type createItemType);

		/// <summary>
		/// Starts to draw.
		/// </summary>
		/// <param name="clickedOn">The item.</param>
		/// <param name="createItemType">The item type.</param>
		/// <param name="panel">The design panel to draw on.</param>
		/// <param name="e">The <see cref="MouseEventArgs"/> argument that initiated the draw operation.</param>
		/// <param name="createItemCallback">Callback used to create the item.</param>
		void StartDrawItem(DesignItem clickedOn, Type createItemType, IDesignPanel panel, PointerEventArgs e, Action<DesignItem> drawItemCallback);
	}
}
