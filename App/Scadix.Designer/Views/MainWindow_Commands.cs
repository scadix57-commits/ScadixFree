using System.IO;
using System.Text;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Scadix.AxamlDesigner;

namespace Scadix.Designer
{
    public partial class MainWindow
    {
        public static SimpleCommand CloseAllCommand = new SimpleCommand("Close All");
        public static SimpleCommand SaveAllCommand = new SimpleCommand("Save All", KeyModifiers.Control | KeyModifiers.Shift, Key.S);
        public static SimpleCommand ExitCommand = new SimpleCommand("Exit");
        public static SimpleCommand RefreshCommand = new SimpleCommand("Refresh", Key.F5);
        public static SimpleCommand RunCommand = new SimpleCommand("Run", KeyModifiers.Shift, Key.F5);
        public static SimpleCommand RenderToBitmapCommand = new SimpleCommand("Render to Bitmap");

        private void RenameCommands()
        {
            // Avalonia handles headers in MenuItem directly
        }

        private void NewCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.New();
        }

        private void OpenCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.Open();
        }

        private void CloseCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.CloseCurrentDocument();
        }

        private void CloseAllCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.CloseAll();
        }

        private void SaveCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.SaveCurrentDocument();
        }

        private void SaveAsCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.SaveCurrentDocumentAs();
        }

        private void SaveAllCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.SaveAll();
        }

        private void RunCommand_Executed(object? sender, RoutedEventArgs e)
        {
            if (MainWindowViewModel.Instance.CurrentDocument == null) return;

            StringBuilder sb = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(new StringWriter(sb)))
            {
                MainWindowViewModel.Instance.CurrentDocument.DesignSurface.SaveDesigner(xmlWriter);
            }

            var txt = sb.ToString();
            var ctl = AvaloniaRuntimeXamlLoader.Parse<Control>(txt);

            Window? wnd = ctl as Window;
            if (wnd == null)
            {
                wnd = new Window();
                wnd.Content = ctl;
            }
            wnd.Show();
        }

        private async void RenderToBitmapCommand_Executed(object? sender, RoutedEventArgs e)
        {
            if (MainWindowViewModel.Instance.CurrentDocument == null) return;

            int desiredWidth = 300;
            int desiredHeight = 300;

            StringBuilder sb = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(new StringWriter(sb)))
            {
                MainWindowViewModel.Instance.CurrentDocument.DesignSurface.SaveDesigner(xmlWriter);
            }

            var txt = sb.ToString();
            var ctl = AvaloniaRuntimeXamlLoader.Parse<Control>(txt);

            // Avalonia rendering to bitmap is different
            var pixelSize = new PixelSize(desiredWidth, desiredHeight);
            var bitmap = new RenderTargetBitmap(pixelSize);

            // We need to measure and arrange the control
            ctl.Measure(new Size(desiredWidth, desiredHeight));
            ctl.Arrange(new Rect(0, 0, desiredWidth, desiredHeight));
            bitmap.Render(ctl);

            var topLevel = GetTopLevel(this);
            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Rendered Bitmap",
                    SuggestedFileName = "Render.png",
                    FileTypeChoices = new[] { new FilePickerFileType("PNG Images") { Patterns = new[] { "*.png" } } }
                });

                if (file != null)
                {
                    using (Stream stm = await file.OpenWriteAsync())
                    {
                        bitmap.Save(stm);
                        stm.Flush();
                    }
                }
            }
        }

        private void ExitCommand_Executed(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.Exit();
        }

        private void RouteDesignSurfaceCommands()
        {
            // In Avalonia, we can bind directly in XAML or Use CommandManager
        }

        private void NuGetPackageManager_Click(object? sender, RoutedEventArgs e)
        {
            MainWindowViewModel.Instance.Factory.OpenNuGetManager(MainWindowViewModel.Instance.SelectedSolutionNode);
        }

        
       

        
    }
}
