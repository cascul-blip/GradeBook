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

    public static async Task<bool> ShowAsync(Window owner, string title, string message, string confirmText = "Delete")
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            MessageText = { Text = message },
            ConfirmButton = { Content = confirmText }
        };

        await dialog.ShowDialog(owner);
        return dialog._confirmed;
    }
}
