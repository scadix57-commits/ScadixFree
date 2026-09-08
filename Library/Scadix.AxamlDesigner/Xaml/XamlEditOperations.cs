
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using System.Collections.ObjectModel;
using Scadix.AxamlDesign;
using Scadix.AxamlDom;

namespace Scadix.AxamlDesigner.Xaml
{
	/// <summary>
	/// Deals with operations on controls which also require access to internal XML properties of the XAML Document.
	/// </summary>
	public class XamlEditOperations
	{
		readonly XamlDesignContext _context;
		readonly XamlParserSettings _settings;
		
		static readonly char _delimeter = Convert.ToChar(0x7F);
		
		/// <summary>
		/// Delimet character to seperate different piece of Xaml's
		/// </summary>
		public char Delimeter {
			get { return _delimeter; }
		}

		public XamlEditOperations(XamlDesignContext context, XamlParserSettings settings)
		{
			this._context = context;
			this._settings = settings;
		}

		static IClipboard? GetClipboard()
		{
			// In Avalonia 12, clipboard is accessed via TopLevel
			// We use a stored reference or try to get it from the application's main window
			if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
				return TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
			return null;
		}
		
		/// <summary>
		/// Copy <paramref name="designItems"/> from the designer to clipboard.
		/// </summary>
		public async void Cut(ICollection<DesignItem> designItems)
		{
			var clipboard = GetClipboard();
			if (clipboard != null) await clipboard.ClearAsync();

			var cutList = RemoveChildItemsWhenContainerIsInList(designItems);

			string cutXaml = "";
			var changeGroup = _context.OpenGroup("Cut " + cutList.Count + "/" + designItems.Count + " elements", cutList);
			foreach (var item in cutList)
			{
				if (item != null && item != _context.RootItem)
				{
					XamlDesignItem? xamlItem = item as XamlDesignItem;
					if (xamlItem != null) {
						cutXaml += XamlStaticTools.GetXaml(xamlItem.XamlObject);
						cutXaml += _delimeter;
					}
				}
			}
			ModelTools.DeleteComponents(cutList);
			if (clipboard != null) await clipboard.SetTextAsync(cutXaml);
			changeGroup.Commit();
		}
		
		/// <summary>
		/// Copy <paramref name="designItems"/> from the designer to clipboard.
		/// </summary>
		public async void Copy(ICollection<DesignItem> designItems)
		{
			var clipboard = GetClipboard();
			if (clipboard != null) await clipboard.ClearAsync();

			var copyList = RemoveChildItemsWhenContainerIsInList(designItems);

			string copiedXaml = "";
			var changeGroup = _context.OpenGroup("Copy " + copyList.Count + "/" + designItems.Count + " elements", copyList);
			foreach (var item in copyList)
			{
				if (item != null)
				{
					XamlDesignItem? xamlItem = item as XamlDesignItem;
					if (xamlItem != null) {
						copiedXaml += XamlStaticTools.GetXaml(xamlItem.XamlObject);
						copiedXaml += _delimeter;
					}
				}
			}
			if (clipboard != null) await clipboard.SetTextAsync(copiedXaml);
			changeGroup.Commit();
		}
		
		/// <summary>
		/// Paste items from clipboard into the PrimarySelection.
		/// </summary>
		public void Paste()
		{
			this.Paste(_context.Services.Selection.PrimarySelection);
		}

		/// <summary>
		/// Paste items from clipboard into the container.
		/// </summary>
		public async void Paste(DesignItem container)
		{
			var parent = container;
			var child = container;

			bool pasted = false;
			var clipboard = GetClipboard();
			string combinedXaml = clipboard != null ? (await clipboard.TryGetTextAsync() ?? "") : "";
			IEnumerable<string> xamls = combinedXaml.Split(_delimeter);
			xamls = xamls.Where(xaml => xaml != "");

			XamlDesignItem? rootItem = parent.Services.DesignPanel.Context.RootItem as XamlDesignItem;
			if (rootItem == null) return;
			var pastedItems = new Collection<DesignItem>();
			foreach (var xaml in xamls)
			{
				var obj = XamlParser.ParseSnippet(rootItem.XamlObject, xaml, _settings);
				if (obj != null)
				{
					DesignItem? item = ((XamlComponentService)parent.Services.Component).RegisterXamlComponentRecursive(obj);
					if (item != null)
						pastedItems.Add(item);
				}
			}

			if (pastedItems.Count != 0)
			{
				var changeGroup = parent.Services.DesignPanel.Context.OpenGroup("Paste " + pastedItems.Count + " elements", pastedItems);
				while (parent != null && pasted == false)
				{
					if (parent.ContentProperty != null)
					{
						if (parent.ContentProperty.IsCollection)
						{
							if (CollectionSupport.CanCollectionAdd(parent.ContentProperty.ReturnType, pastedItems.Select(item => item.Component)) && parent.GetBehavior<IPlacementBehavior>() != null)
							{
								AddInParent(parent, pastedItems);
								pasted = true;
							}
						}
						else if (pastedItems.Count == 1 && parent.ContentProperty.Value == null && parent.ContentProperty.ValueOnInstance == null && parent.View is ContentControl)
						{
							AddInParent(parent, pastedItems);
							pasted = true;
						}
						if (!pasted)
							parent = parent.Parent;
					}
					else
					{
						parent = parent.Parent;
					}
				}

				while (pasted == false)
				{
					if (child.ContentProperty != null)
					{
						if (child.ContentProperty.IsCollection)
						{
							foreach (var col in child.ContentProperty.CollectionElements)
							{
								if (col.ContentProperty != null && col.ContentProperty.IsCollection)
								{
									if (CollectionSupport.CanCollectionAdd(col.ContentProperty.ReturnType, pastedItems.Select(item => item.Component)))
									{
										pasted = true;
									}
								}
							}
							break;
						}
						else if (child.ContentProperty.Value != null)
						{
							child = child.ContentProperty.Value;
						}
						else if (pastedItems.Count == 1)
						{
							child.ContentProperty.SetValue(pastedItems.First().Component);
							pasted = true;
							break;
						}
						else
							break;
					}
					else
						break;
				}

				foreach (var pastedItem in pastedItems)
				{
					((XamlComponentService)parent.Services.Component).RaiseComponentRegisteredAndAddedToContainer(pastedItem);
				}

				changeGroup.Commit();
			}
		}

		/// <summary>
		/// Adds Items under a parent given that the content property is collection and can add types of <paramref name="pastedItems"/>
		/// </summary>
		static void AddInParent(DesignItem parent, IList<DesignItem> pastedItems)
		{
			IEnumerable<Rect> rects = pastedItems.Select(i => new Rect(new Point(0, 0), new Point(i.Properties["Width"].GetConvertedValueOnInstance<double>(), i.Properties["Height"].GetConvertedValueOnInstance<double>())));
			var operation = PlacementOperation.TryStartInsertNewComponents(parent, pastedItems, rects.ToList(), PlacementType.PasteItem);
			ISelectionService selection = parent.Services.DesignPanel.Context.Services.Selection;
			selection.SetSelectedComponents(pastedItems);
			if (operation != null)
				operation.Commit();
		}

		List<DesignItem> RemoveChildItemsWhenContainerIsInList(ICollection<DesignItem> designItems)
		{
			var copyList = designItems.ToList();
			foreach (var designItem in designItems)
			{
				var parent = designItem.Parent;
				while (parent != null)
				{
					if (copyList.Contains(parent))
					{
						copyList.Remove(designItem);
					}
					parent = parent.Parent;
				}
			}

			return copyList;
		}
	}
}
