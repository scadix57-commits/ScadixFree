using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Scadix.Designer.Views;

public enum SaveConfirmationResult { Save, Discard, Cancel }

public partial class SaveConfirmationDialog : Window
{
    public SaveConfirmationResult Result { get; private set; } = SaveConfirmationResult.Cancel;

    public SaveConfirmationDialog(string message)
    {
        InitializeComponent();

        this.FindControl<TextBlock>("MessageText")!.Text = message;

        this.FindControl<Button>("SaveButton")!.Click += (_, _) =>
        {
            Result = SaveConfirmationResult.Save;
            Close();
        };

        this.FindControl<Button>("DiscardButton")!.Click += (_, _) =>
        {
            Result = SaveConfirmationResult.Discard;
            Close();
        };

        this.FindControl<Button>("CancelButton")!.Click += (_, _) =>
        {
            Result = SaveConfirmationResult.Cancel;
            Close();
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
