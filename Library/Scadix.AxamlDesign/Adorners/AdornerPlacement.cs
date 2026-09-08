

using Avalonia;
using Avalonia.Controls;

namespace Scadix.AxamlDesign.Adorners
{
	/// <summary>
	/// Defines how a design-time adorner is placed.
	/// </summary>
	public abstract class AdornerPlacement
	{
		/// <summary>
		/// A placement instance that places the adorner above the content, using the same bounds as the content.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
		public static readonly AdornerPlacement FillContent = new FillContentPlacement();
		
		/// <summary>
		/// Arranges the adorner element on the specified adorner panel.
		/// </summary>
		public abstract void Arrange(AdornerPanel panel, Control adorner, Size adornedElementSize);
		
		sealed class FillContentPlacement : AdornerPlacement
		{
			public override void Arrange(AdornerPanel panel, Control adorner, Size adornedElementSize)
			{
				adorner.Arrange(new Rect(adornedElementSize));
			}
		}
	}
}
