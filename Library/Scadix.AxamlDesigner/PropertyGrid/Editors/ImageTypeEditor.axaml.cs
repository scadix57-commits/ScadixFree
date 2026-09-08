using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Scadix.AxamlDesign.PropertyGrid;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
    [TypeEditor(typeof(IImage))]
    public partial class ImageTypeEditor : UserControl
    {
        public PropertyNode PropertyNode => DataContext as PropertyNode;

        public ImageTypeEditor()
        {
            InitializeComponent();

            ImagePickerControlView.uxOk.Click += UxOk_Click;
            var cancelBtn = ImagePickerControlView.FindControl<Button>("uxCancel");
            if (cancelBtn != null)
                cancelBtn.Click += UxCancel_Click;
        }

        private void UxOk_Click(object? sender, RoutedEventArgs e)
        {
            var propertyNode = DataContext as PropertyNode;
            if (propertyNode == null) return;
            
            if (ImagePickerControlView.SelectedImagePath is string finalPath && !string.IsNullOrEmpty(finalPath))
            {
                // حفظ المسار كنص لكي لا يتحول إلى النوع Avalonia.Media.Imaging.Bitmap
                propertyNode.FirstProperty.SetValue(finalPath);
                
                // إغلاق النافذة
                var btn = this.FindControl<Button>("uxFlyoutButton");
                btn?.Flyout?.Hide();
            }
        }

        private void UxCancel_Click(object? sender, RoutedEventArgs e)
        {
            var btn = this.FindControl<Button>("uxFlyoutButton");
            btn?.Flyout?.Hide();
        }
    }
}
