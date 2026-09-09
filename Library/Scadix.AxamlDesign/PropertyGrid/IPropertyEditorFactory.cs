using Avalonia.Controls;

namespace Scadix.AxamlDesign.PropertyGrid;

/// <summary>Allows a host to supply editors for its own persistence model.</summary>
public interface IPropertyEditorFactory
{
    Control CreateEditor(PropertyNode node);
}
