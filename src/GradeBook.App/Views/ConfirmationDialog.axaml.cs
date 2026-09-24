using Avalonia.Controls;

namespace GradeBook.App.Views;

public partial class ConfirmationDialog : Window
{
    private bool _confirmed;

    public ConfirmationDialog()
    {
        InitializeComponent();
        CancelButton.Click += (_, _) => Close();
        ConfirmButton.Click += (_, _) =>
        {
            _confirmed = true;
            Close();
        };
    }

    /// <summary>Returns true only if the confirm button was clicked — closing the window counts as cancel.</summary>
    public static async Task<bool> ShowAsync(Window owner, string title, string message, string confirmText = "Delete", string cancelText = "Cancel")
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            MessageText = { Text = message },
            ConfirmButton = { Content = confirmText },
            CancelButton = { Content = cancelText }
        };

        await dialog.ShowDialog(owner);
        return dialog._confirmed;
    }

    /// <summary>A plain message with a single OK button.</summary>
    public static async Task ShowMessageAsync(Window owner, string title, string message)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            MessageText = { Text = message },
            ConfirmButton = { Content = "OK" },
            CancelButton = { IsVisible = false }
        };

        await dialog.ShowDialog(owner);
    }
}
