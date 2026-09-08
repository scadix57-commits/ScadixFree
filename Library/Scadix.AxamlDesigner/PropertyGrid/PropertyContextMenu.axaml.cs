using Avalonia.Controls;
using Avalonia.Interactivity;
using Scadix.AxamlDesign.PropertyGrid;

namespace Scadix.AxamlDesigner.PropertyGrid
{
    public partial class PropertyContextMenu : UserControl
    {
        public PropertyContextMenu()
        {
            InitializeComponent();
        }

        public PropertyNode PropertyNode {
            get { return DataContext as PropertyNode; }
        }

        public void OpenAt(Control target, PropertyNode node)
        {
            DataContext = node;
            if (ContextMenu != null)
            {
                ContextMenu.DataContext = node;
                ContextMenu.Open(target);
            }
        }

        void Click_Reset(object sender, RoutedEventArgs e)
        {
            PropertyNode?.Reset(); ContextMenu.Close();
        }

        void Click_Binding(object sender, RoutedEventArgs e)
        {
            PropertyNode?.CreateBinding(); ContextMenu.Close();
        }

        void Click_CustomExpression(object sender, RoutedEventArgs e)
        {
        }

        void Click_ConvertToLocalValue(object sender, RoutedEventArgs e)
        {
        }

        void Click_SaveAsResource(object sender, RoutedEventArgs e)
        {
        }
    }
}
