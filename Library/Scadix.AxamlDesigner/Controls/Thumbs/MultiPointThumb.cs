
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesigner.Extensions;

namespace Scadix.AxamlDesigner.Controls.Thumbs
{
	/// <summary>
	/// Description of MultiPointThumb.
	/// </summary>
	internal sealed class MultiPointThumb : DesignerThumb
	{
		private int _index;

		public int Index
		{
			get { return _index; }
			set
			{
				_index = value;
				var p = AdornerPlacement as PointTrackerPlacementSupport;
				if (p != null)
					p.Index = value;
			}
		}

		private AdornerPlacement _adornerPlacement;

		public AdornerPlacement AdornerPlacement
		{
			get { return _adornerPlacement; }
			set { _adornerPlacement = value; }
		}
	}
}
