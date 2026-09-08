using Avalonia.Input.Platform;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Services;
using Scadix.AxamlDesigner.Xaml;

namespace Scadix.AxamlDesigner.Services
{
	public class CopyPasteService : ICopyPasteService
	{
		public virtual bool CanCopy(DesignContext designContext)
		{
			ISelectionService selectionService = designContext.Services.GetService<ISelectionService>();
			if (selectionService != null)
			{
				if (selectionService.SelectedItems.Count == 0)
					return false;
				if (selectionService.SelectedItems.Contains(designContext.RootItem))
					return false;
			}
			return true;
		}

		public virtual void Copy(DesignContext designContext)
		{
			XamlDesignContext xamlContext = designContext as XamlDesignContext;
			ISelectionService selectionService = designContext.Services.GetService<ISelectionService>();
			if (xamlContext != null && selectionService != null && !selectionService.SelectedItems.Contains(designContext.RootItem))
			{
				xamlContext.XamlEditAction.Copy(selectionService.SelectedItems);
			}
		}

		public virtual bool CanCut(DesignContext designContext)
		{
			return CanCopy(designContext);
		}

		public virtual void Cut(DesignContext designContext)
		{
			XamlDesignContext xamlContext = designContext as XamlDesignContext;
			ISelectionService selectionService = designContext.Services.GetService<ISelectionService>();
			if (xamlContext != null && selectionService != null)
			{
				xamlContext.XamlEditAction.Cut(selectionService.SelectedItems);
			}
		}

		public virtual bool CanDelete(DesignContext designContext)
		{
			if (designContext != null)
			{
				return ModelTools.CanDeleteComponents(designContext.Services.Selection.SelectedItems);
			}
			return false;
		}

		public virtual void Delete(DesignContext designContext)
		{
			if (designContext != null)
			{
				ModelTools.DeleteComponents(designContext.Services.Selection.SelectedItems);
			}
		}

		public virtual bool CanPaste(DesignContext designContext)
		{
			ISelectionService selectionService = designContext.Services.GetService<ISelectionService>();
			if (selectionService != null && selectionService.SelectedItems.Count != 0)
			{
				try
				{
					IClipboard? clipboard = null;
					if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
						clipboard = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
					if (clipboard != null)
					{
						var text = clipboard.TryGetTextAsync().GetAwaiter().GetResult();
						if (!string.IsNullOrWhiteSpace(text))
							return true;
					}
				}
				catch (Exception)
				{
				}
			}
			return false;
		}

		public virtual void Paste(DesignContext designContext)
		{
			XamlDesignContext xamlContext = designContext as XamlDesignContext;
			if (xamlContext != null)
			{
				xamlContext.XamlEditAction.Paste();
			}
		}
	}
}
