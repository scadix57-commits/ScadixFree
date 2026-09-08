 
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.PropertyGrid.Editors;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Extends the Quick operation menu for the designer.
	/// </summary>
	[ExtensionServer(typeof(OnlyOneItemSelectedExtensionServer))]
	[ExtensionFor(typeof (Control))]
	public class QuickOperationMenuExtension : PrimarySelectionAdornerProvider
	{
		private QuickOperationMenu _menu;
		private KeyBinding _keyBinding;
		
		protected override void OnInitialized()
		{
			base.OnInitialized();
			_menu = new QuickOperationMenu();
			_menu.Loaded += OnMenuLoaded;
			_menu.RenderTransform = new Avalonia.Media.MatrixTransform(this.ExtendedItem.GetCompleteAppliedTransformationToView().Value.Invert());
			var placement = new RelativePlacement(HorizontalAlignment.Right, VerticalAlignment.Top) {XOffset = 7, YOffset = 3.5};
			this.AddAdorners(placement, _menu);
			
			var kbs = this.ExtendedItem.Services.GetService(typeof (IKeyBindingService)) as IKeyBindingService;
			var command = new RelayCommand(delegate
			                                {
			                                	if (_menu.MainHeader != null) {
			                                		_menu.MainHeader.Focus();
			                                	}
			                                });
			_keyBinding = new KeyBinding { Command = command, Gesture = new KeyGesture(Key.Enter, KeyModifiers.Alt) };
			if (kbs != null)
				kbs.RegisterBinding(_keyBinding);
		}

		private void OnMenuLoaded(object sender, EventArgs e)
		{
			if(_menu.MainHeader!=null)
				_menu.MainHeader.Click += MainHeaderClick;
			
			int menuItemsAdded = 0;
			var view = this.ExtendedItem.View;

			if (view != null) {
				string setValue;
				if(view is ItemsControl) {
					_menu.AddSubMenuInTheHeader(new MenuItem() {Header = "Edit Items"});
				}
				
				if(view is Grid) {
					_menu.AddSubMenuInTheHeader(new MenuItem() {Header = "Edit Rows"});
					_menu.AddSubMenuInTheHeader(new MenuItem() {Header = "Edit Columns"});
				}
				
				if (view is StackPanel) {
					var ch = new MenuItem() {Header = "Change Orientation"};
					_menu.AddSubMenuInTheHeader(ch);
					setValue = this.ExtendedItem.Properties[StackPanel.OrientationProperty].GetConvertedValueOnInstance<object>().ToString();
					_menu.AddSubMenuCheckable(ch, Enum.GetValues(typeof (Orientation)), Orientation.Vertical.ToString(), setValue);
					_menu.MainHeader.Items.Add(new Separator());
					menuItemsAdded++;
				}
				
				if(this.ExtendedItem.Parent!=null && this.ExtendedItem.Parent.View is DockPanel) {
					var sda = new MenuItem() {Header = "Set Dock to"};
					_menu.AddSubMenuInTheHeader(sda);
					setValue = this.ExtendedItem.Properties.GetAttachedProperty(DockPanel.DockProperty).GetConvertedValueOnInstance<object>().ToString();
					_menu.AddSubMenuCheckable(sda, Enum.GetValues(typeof (Dock)), Dock.Left.ToString(), setValue);
					_menu.MainHeader.Items.Add(new Separator());
					menuItemsAdded++;
				}

				var ha = new MenuItem() {Header = "Horizontal Alignment"};
				_menu.AddSubMenuInTheHeader(ha);
				setValue = this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].GetConvertedValueOnInstance<object>().ToString();
				_menu.AddSubMenuCheckable(ha, Enum.GetValues(typeof (HorizontalAlignment)), HorizontalAlignment.Stretch.ToString(), setValue);
				menuItemsAdded++;

				var va = new MenuItem() {Header = "Vertical Alignment"};
				_menu.AddSubMenuInTheHeader(va);
				setValue = this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].GetConvertedValueOnInstance<object>().ToString();
				_menu.AddSubMenuCheckable(va, Enum.GetValues(typeof (VerticalAlignment)), VerticalAlignment.Stretch.ToString(), setValue);
				menuItemsAdded++;
			}

			if (menuItemsAdded == 0) {
				OnRemove();
			}
		}

		private void MainHeaderClick(object sender, RoutedEventArgs e)
		{
			var clickedOn = e.Source as MenuItem;
			if (clickedOn != null) {
				var parent = clickedOn.Parent as MenuItem;
				if (parent != null) {
					
					if((string)clickedOn.Header=="Edit Items") {
						var editor = new CollectionEditor(TopLevel.GetTopLevel(this.ExtendedItem.View) as Window);
						var itemsControl=this.ExtendedItem.View as ItemsControl;
						if (itemsControl != null)
							editor.LoadItemsCollection(this.ExtendedItem);
						editor.Show();
					}
					
					if((string)clickedOn.Header=="Edit Rows") {
						var editor = new FlatCollectionEditor(TopLevel.GetTopLevel(this.ExtendedItem.View) as Window);
						var gd=this.ExtendedItem.View as Grid;
						if (gd != null)
							editor.LoadItemsCollection(this.ExtendedItem.Properties["RowDefinitions"]);
						editor.Show();
					}
					
					if((string)clickedOn.Header=="Edit Columns") {
						var editor = new FlatCollectionEditor(TopLevel.GetTopLevel(this.ExtendedItem.View) as Window);
						var gd=this.ExtendedItem.View as Grid;
						if (gd != null)
							editor.LoadItemsCollection(this.ExtendedItem.Properties["ColumnDefinitions"]);
						editor.Show();
					}
					
					if (parent.Header is string && (string) parent.Header == "Change Orientation") {
						var value = _menu.UncheckChildrenAndSelectClicked(parent, clickedOn);
						if (value != null) {
							var orientation = Enum.Parse(typeof (Orientation), value);
							if (orientation != null)
								this.ExtendedItem.Properties[StackPanel.OrientationProperty].SetValue(orientation);
						}
					}
					if (parent.Header is string && (string)parent.Header == "Set Dock to") {
						var value = _menu.UncheckChildrenAndSelectClicked(parent, clickedOn);
						if(value!=null) {
							var dock = Enum.Parse(typeof (Dock), value);
							if (dock != null)
								this.ExtendedItem.Properties.GetAttachedProperty(DockPanel.DockProperty).SetValue(dock);
						}
					}
					

					if (parent.Header is string && (string) parent.Header == "Horizontal Alignment") {
						var value = _menu.UncheckChildrenAndSelectClicked(parent, clickedOn);
						if (value != null) {
							var ha = Enum.Parse(typeof (HorizontalAlignment), value);
							if (ha != null)
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].SetValue(ha);
						}
					}

					if (parent.Header is string && (string) parent.Header == "Vertical Alignment") {
						var value = _menu.UncheckChildrenAndSelectClicked(parent, clickedOn);
						if (value != null) {
							var va = Enum.Parse(typeof (VerticalAlignment), value);
							if (va != null)
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].SetValue(va);
						}
					}
				}
			}
		}

		protected override void OnRemove()
		{
			base.OnRemove();
			_menu.Loaded -= OnMenuLoaded;
			var kbs = this.ExtendedItem.Services.GetService(typeof (IKeyBindingService)) as IKeyBindingService;
			if(kbs!=null)
				kbs.DeregisterBinding(_keyBinding);
		}
	}
}
