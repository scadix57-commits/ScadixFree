

using Avalonia.Controls;
using Avalonia.Media;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// A button with a drop-down arrow.
	/// </summary>
	public class DropDownButton : Button
	{
		static readonly Geometry triangle = Geometry.Parse("M0,0 L1,0 0.5,1 z");
		
		public DropDownButton()
		{
			Content = new Avalonia.Controls.Shapes.Path {
				Fill = Brushes.Black,
				Data = triangle,
				Width = 7, 
				Height = 3.5,
				Stretch = Stretch.Fill
			};
		}
	}
}
