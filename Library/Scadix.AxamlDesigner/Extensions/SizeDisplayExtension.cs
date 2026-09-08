 

using Avalonia.Controls;
using Avalonia.Layout;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Display Height/Width on the primary selection
	/// </summary>
	[ExtensionFor(typeof(Control))]
	public class SizeDisplayExtension : PrimarySelectionAdornerProvider
	{
		HeightDisplay _heightDisplay;
		WidthDisplay _widthDisplay;
		
		public HeightDisplay HeightDisplay{
			get { return _heightDisplay; }
		}
		
		public WidthDisplay WidthDisplay{
			get { return _widthDisplay; }
		}
		
		protected override void OnInitialized()
		{
			base.OnInitialized();
			if (this.ExtendedItem != null)
			{
				RelativePlacement placementHeight = new RelativePlacement(HorizontalAlignment.Right, VerticalAlignment.Stretch);
				placementHeight.XOffset = 10;
				_heightDisplay = new HeightDisplay();
				_heightDisplay.DataContext = this.ExtendedItem.Component;

				RelativePlacement placementWidth = new RelativePlacement(HorizontalAlignment.Stretch, VerticalAlignment.Bottom);
				placementWidth.YOffset = 10;
				_widthDisplay = new WidthDisplay();
				_widthDisplay.DataContext = this.ExtendedItem.Component;

				this.AddAdorners(placementHeight, _heightDisplay);
				this.AddAdorners(placementWidth, _widthDisplay);
				_heightDisplay.IsVisible = false;
				_widthDisplay.IsVisible = false;
			}
		}
	}
}

