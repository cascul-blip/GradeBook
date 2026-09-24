using System.Globalization;
using Avalonia.Controls;
using GradeBook.App.ViewModels;
using GradeBook.Core.Models;

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

        QuarterBox.ItemsSource = GradebookViewModel.Quarters;

        CancelButton.Click += (_, _) => Close();

        SaveButton.Click += (_, _) =>
        {
            ErrorText.IsVisible = false;

            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ShowError("Enter a name.");
                return;
            }

            if (!TryParsePoints(PointsBox.Text, out var points) || points <= 0)
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

    private static bool TryParsePoints(string? text, out decimal points) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.InvariantCulture, out points);

    public static async Task<(EditAssignmentDialogResult Result, string Name, decimal PointsPossible, Quarter Quarter)> ShowAsync(
        Window owner, string currentName, decimal currentPoints, Quarter currentQuarter)
    {
        var dialog = new EditAssignmentDialog();
        dialog.NameBox.Text = currentName;
        dialog.PointsBox.Text = currentPoints.ToString("G29", CultureInfo.InvariantCulture);
        dialog.QuarterBox.SelectedItem = currentQuarter;

        await dialog.ShowDialog(owner);

        TryParsePoints(dialog.PointsBox.Text, out var parsedPoints);
        var quarter = dialog.QuarterBox.SelectedItem is Quarter q ? q : currentQuarter;
        return (dialog._result, dialog.NameBox.Text?.Trim() ?? currentName, parsedPoints, quarter);
    }
}
