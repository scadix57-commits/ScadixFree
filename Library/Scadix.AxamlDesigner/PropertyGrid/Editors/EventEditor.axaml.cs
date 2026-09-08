

using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	[TypeEditor(typeof(MulticastDelegate))]
	public partial class EventEditor : UserControl
    {
		public EventEditor()
		{
			InitializeComponent();
			txtEventHandler.LostFocus += (s, e) =>
			{
				if (PropertyNode != null && txtEventHandler.Text != ValueString)
				{
					Commit();
				}
			};
			txtEventHandler.KeyDown += OnKeyDown;
			txtEventHandler.DoubleTapped += OnMouseDoubleClick;
		}
 

        public PropertyNode PropertyNode {
			get { return DataContext as PropertyNode; }
		}

		public string ValueString {
			get { return (string)PropertyNode.Value ?? ""; }
		}

		protected  void OnKeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter) {
				Commit();
			}
			else if (e.Key == Key.Escape) {
                txtEventHandler.Text = ValueString;
			}
		}

		protected  void OnMouseDoubleClick(object? sender, TappedEventArgs e)
		{
			IEventHandlerService s = PropertyNode.Services.GetService<IEventHandlerService>();
			if (s != null && string.IsNullOrEmpty(txtEventHandler.Text)) {
				s.CreateEventHandler(PropertyNode.FirstProperty);
			}
			else {
				Commit();
			}
		}

		public void Commit()
		{		
			if (txtEventHandler.Text != ValueString) {
				if (string.IsNullOrEmpty(txtEventHandler.Text)) {
					PropertyNode.Reset();
					return;
				}
				PropertyNode.Value = txtEventHandler.Text;
			}
			IEventHandlerService s = PropertyNode.Services.GetService<IEventHandlerService>();
			if (s != null) {
				s.CreateEventHandler(PropertyNode.FirstProperty);
			}
		}
	}
}
