

using Avalonia;
using System.Diagnostics;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;

namespace Scadix.AxamlDesigner.Controls.Thumbs
{
	/// <summary>
	/// Resize thumb that automatically disappears if the adornered element is too small.
	/// </summary>
	public sealed class ResizeThumb : DesignerThumb
	{
		bool checkWidth, checkHeight;

		public ResizeThumb(bool checkWidth, bool checkHeight)
		{
			Debug.Assert((checkWidth && checkHeight) == false);
			this.checkWidth = checkWidth;
			this.checkHeight = checkHeight;
		}

		protected override Size ArrangeOverride(Size arrangeBounds)
		{
			AdornerPanel parent = this.Parent as AdornerPanel;
			if (parent != null && parent.AdornedElement != null)
			{
				if (checkWidth)
					this.ThumbVisible = PlacementOperation.GetRealElementSize(parent.AdornedElement).Width > 14;
				else if (checkHeight)
					this.ThumbVisible = PlacementOperation.GetRealElementSize(parent.AdornedElement).Height > 14;
			}
			return base.ArrangeOverride(arrangeBounds);
		}
	}
}
