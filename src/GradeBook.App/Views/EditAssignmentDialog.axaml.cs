using System.Globalization;
using Avalonia.Controls;

namespace GradeBook.App.Views;

public enum EditAssignmentDialogResult
{
    Cancelled,
    Saved,
    Deleted
}

public partial class EditAssignmentDialog : Window
{
    private EditAssignmentDialogResult _result = EditAssignmentDialogResult.Cancelled;

    public EditAssignmentDialog()
    {
        InitializeComponent();

        CancelButton.Click += (_, _) => Close();

        SaveButton.Click += (_, _) =>
        {
            ErrorText.IsVisible = false;

            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ShowError("Enter a name.");
                return;
            }

            if (!decimal.TryParse(PointsBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var points) || points <= 0)
            {
                ShowError("Enter a positive point value.");
                return;
            }

            _result = EditAssignmentDialogResult.Saved;
            Close();
        };

        DeleteButton.Click += async (_, _) =>
        {
            var confirmed = await ConfirmationDialog.ShowAsync(
                this, "Delete Lesson", $"Permanently delete \"{NameBox.Text}\" and every grade recorded for it? This cannot be undone.");
            if (confirmed)
            {
                _result = EditAssignmentDialogResult.Deleted;
                Close();
            }
        };
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    public static async Task<(EditAssignmentDialogResult Result, string Name, decimal PointsPossible)> ShowAsync(
        Window owner, string currentName, decimal currentPoints)
    {
        var dialog = new EditAssignmentDialog();
        dialog.NameBox.Text = currentName;
        dialog.PointsBox.Text = currentPoints.ToString(CultureInfo.InvariantCulture);

        await dialog.ShowDialog(owner);

        decimal.TryParse(dialog.PointsBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedPoints);
        return (dialog._result, dialog.NameBox.Text?.Trim() ?? currentName, parsedPoints);
    }
}
