 
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Scadix.AxamlDesigner.Controls
{
	public class HeightDisplay : TemplatedControl
	{
		protected override Type StyleKeyOverride => typeof(HeightDisplay);
	}

	public class WidthDisplay : TemplatedControl
	{
		protected override Type StyleKeyOverride => typeof(WidthDisplay);
	}
}
