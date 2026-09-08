

using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Scadix.AxamlDesign.UIExtensions
{
	/// <summary>
	/// Contains helper methods for UI. 
	/// </summary>
	public static class UIHelpers
	{
		/// <summary>
		/// Gets the parent. Which tree the parent is retrieved from depends on the parameters.
		/// </summary>
		/// <param name="child">The child to get parent for.</param>
		/// <param name="searchCompleteVisualTree">If true the parent in the visual tree is returned, if false the parent may be retrieved from another tree depending on the child type.</param>
		/// <returns>The parent element, and depending on the parameters its retrieved from either visual tree, logical tree or a tree not strictly speaking either the logical tree or the visual tree.</returns>
		public static AvaloniaObject GetParentObject(this AvaloniaObject child, bool searchCompleteVisualTree)
		{
			if (child == null) return null;

			if (!searchCompleteVisualTree) {
				var contentElement = child as StyledElement;
				if (contentElement != null)
				{
					AvaloniaObject parent = contentElement.Parent;
					if (parent != null) return parent;
	
					var fce = contentElement as StyledElement;
					return fce != null ? fce.Parent : null;
				}
	
				var Control = child as Control;
				if (Control != null)
				{
					AvaloniaObject parent = Control.Parent;
					if (parent != null) return parent;
				}
                if (child is ILogical logicalElement)
                {
                    var logicalParent = logicalElement.LogicalParent;
                    if (logicalParent is AvaloniaObject parentObj) return parentObj;
                }
            }
            if (child is Visual visual)
            {
                return visual.GetVisualParent() as AvaloniaObject;
            }

            return null;
		}

		/// <summary>
		/// Gets first parent element of the specified type. Which tree the parent is retrieved from depends on the parameters.
		/// </summary>
		/// <param name="child">The child to get parent for.</param>
		/// <param name="searchCompleteVisualTree">If true the parent in the visual tree is returned, if false the parent may be retrieved from another tree depending on the child type.</param>
		/// <returns>
		/// The first parent element of the specified type, and depending on the parameters its retrieved from either visual tree, logical tree or a tree not strictly speaking either the logical tree or the visual tree.
		/// null is returned if no parent of the specified type is found.
		/// </returns>
		public static T TryFindParent<T>(this AvaloniaObject child, bool searchCompleteVisualTree = false) where T : AvaloniaObject
		{
			AvaloniaObject parentObject = GetParentObject(child, searchCompleteVisualTree);

			if (parentObject == null) return null;

			T parent = parentObject as T;
			if (parent != null)
			{
				return parent;
			}

			return TryFindParent<T>(parentObject);
		}

		/// <summary>
		/// Returns the first child of the specified type found in the visual tree.
		/// </summary>
		/// <param name="parent">The parent element where the search is started.</param>
		/// <returns>The first child of the specified type found in the visual tree, or null if no parent of the specified type is found.</returns>
		public static T TryFindChild<T>(this AvaloniaObject parent) where T : AvaloniaObject
		{
            
            if (parent is not Visual visualParent) return null;

            
            var children = visualParent.GetVisualChildren();

            foreach (var visualChild in children)
            {
               
                if (visualChild is AvaloniaObject child)
                {
                    if (child is T)
                    {
                        return (T)child;
                    }
                    child = TryFindChild<T>(child);
                    if (child != null)
                    {
                        return (T)child;
                    }
                }
            }

			return null;
		}

		/// <summary>
		/// Returns the first child of the specified type and with the specified name found in the visual tree.
		/// </summary>
		/// <param name="parent">The parent element where the search is started.</param>
		/// <param name="childName">The name of the child element to find, or an empty string or null to only look at the type.</param>
		/// <returns>The first child that matches the specified type and child name, or null if no match is found.</returns>
		public static T TryFindChild<T>(this AvaloniaObject parent, string childName) where T : AvaloniaObject
		{
            if (parent == null || parent is not Visual visualParent) return null;
            T foundChild = null;
			var childrenCount = visualParent.GetVisualChildren();
			foreach (var child in childrenCount)
			{

                var childType = child as T;
                if (childType == null)
                {
                    foundChild = TryFindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var control = child as Control;
                    if (control != null && control.Name == childName)
                    {
                        foundChild = child as T;
                        break;
                    }
                    foundChild = TryFindChild<T>(child as AvaloniaObject, childName);
                    if (foundChild != null) break;
                }
                else
                {
                    foundChild = child as T;
                    break;
                }

            }
          
            return foundChild;
		}

		/// <summary>
		///   Returns the first ancestor of specified type
		/// </summary>
		public static T FindAncestor<T>(AvaloniaObject current) where T : AvaloniaObject
		{
			current = GetVisualOrLogicalParent(current);

			while (current != null)
			{
				if (current is T)
				{
					return (T)current;
				}
				current = GetVisualOrLogicalParent(current);
			}

			return null;
		}

        private static AvaloniaObject GetVisualOrLogicalParent(AvaloniaObject obj)
        {
            if (obj is Visual visual)
            {
                var visualParent = visual.GetVisualParent();
                if (visualParent != null) return visualParent as AvaloniaObject;
            }

            if (obj is ILogical logical)
            {
                return logical.LogicalParent as AvaloniaObject;
            }

            return null;
        }
    }
}
