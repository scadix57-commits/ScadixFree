 

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	public partial class ComboBoxEditor: NullableComboBox
    {
		/// <summary>
		/// Create a new ComboBoxEditor instance.
		/// </summary>
		public ComboBoxEditor()
		{
			InitializeComponent();
		}
		
		/// <inheritdoc/>
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
		 
            base.OnApplyTemplate(e);
            var popup = e.NameScope.Find<Popup>("PART_Popup");
            if (popup != null)
            {
                // In Avalonia, FontWeight is handled differently
                popup.SetValue(FontWeightProperty, Avalonia.Media.FontWeight.Normal);
            }
        }
	}
}
