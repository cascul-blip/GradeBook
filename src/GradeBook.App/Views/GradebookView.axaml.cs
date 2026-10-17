using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using GradeBook.App.Converters;
using GradeBook.App.ViewModels;
using GradeBook.Core.GradeCalculation;
using GradeBook.Core.Models;

namespace GradeBook.App.Views;

public partial class GradebookView : UserControl
{
    // Wide enough to fit the longest status option ("Uncompleted") plus the ComboBox's dropdown arrow;
    // the score box matches it so the two controls line up visually within a cell.
    private const double CellControlWidth = 120;

    private static readonly IBrush InvalidScoreBrush = Brushes.Red;
    private static readonly IBrush AboveMaxScoreBrush = Brushes.Orange;
    private static readonly IBrush LateScoreBrush = Brushes.DodgerBlue;

    private GradebookViewModel? _subscribedViewModel;

    // Keyed by (row, column index) so Tab/Shift+Tab can jump straight to a specific cell's control
    // without relying on the DataGrid's own row-major cell navigation. Rebuilt on every column rebuild.
    private readonly Dictionary<(GradebookRowViewModel Row, int ColumnIndex), TextBox> _scoreBoxes = new();
    private readonly Dictionary<(GradebookRowViewModel Row, int ColumnIndex), ComboBox> _statusBoxes = new();

    public GradebookView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => OnDataContextChanged();
    }

    private void OnDataContextChanged()
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Assignments.CollectionChanged -= OnAssignmentsChanged;
        }

        _subscribedViewModel = DataContext as GradebookViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Assignments.CollectionChanged += OnAssignmentsChanged;
            RebuildColumns();
        }
    }

    private void OnAssignmentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildColumns();

    /// <summary>
    /// Avalonia's DataGrid doesn't support purely-declarative dynamic columns for a variable-length
    /// assignment list, so columns are rebuilt in code whenever the assignment set changes. Each cell's
    /// content is built via a FuncDataTemplate closing over the column index into that row's Cells list.
    /// </summary>
    private void RebuildColumns()
    {
        if (_subscribedViewModel is not { } viewModel)
        {
            return;
        }

        GradeGrid.Columns.Clear();
        _scoreBoxes.Clear();
        _statusBoxes.Clear();

        GradeGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Student",
            Binding = new Binding(nameof(GradebookRowViewModel.StudentName)),
            IsReadOnly = true,
            Width = new DataGridLength(150)
        });

        var assignments = viewModel.Assignments;
        for (var i = 0; i < assignments.Count; i++)
        {
            var index = i;
            var assignment = assignments[i];

            // Edit button goes first (left) so it stays visible/clickable even when a long lesson
            // name would otherwise push it past the column's edge and get clipped.
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var editButton = new Button { Content = "Edit", Padding = new Avalonia.Thickness(6, 0), FontSize = 10 };
            editButton.Click += async (_, _) => await EditAssignmentAsync(viewModel, assignment);
            headerPanel.Children.Add(editButton);
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"{assignment.Name} ({assignment.PointsPossible:0.##} pts)",
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            });

            // Column-major Tab order: from a score box, Tab goes to that row's status dropdown, then
            // the next row's score box in the same column (wrapping to the next column at the last row).
            GradeGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = headerPanel,
                Width = new DataGridLength(140),
                CellTemplate = new FuncDataTemplate<GradebookRowViewModel>((row, _) =>
                {
                    if (row is null || index >= row.Cells.Count)
                    {
                        return new TextBlock();
                    }

                    if (row.Cells[index] is not { } cell)
                    {
                        // No grade row for this student/lesson — show it read-only rather than as an
                        // editable cell whose saves would go nowhere.
                        return new TextBlock { Text = "—", Margin = new Avalonia.Thickness(8, 4) };
                    }

                    var scoreBox = new TextBox { PlaceholderText = "score", Width = CellControlWidth };
                    scoreBox.Bind(TextBox.TextProperty, new Binding(nameof(GradeCellViewModel.Score))
                    {
                        Source = cell,
                        Mode = BindingMode.TwoWay,
                        Converter = DecimalScoreConverter.Instance
                    });

                    // Red border while the text isn't a valid score (it isn't saved). Blue border when the
                    // lesson is late: the box still shows the typed score, and reports subtract 10% of the
                    // points possible, rounded up. Amber border + tooltip when the saved score is above the
                    // points possible (allowed as extra credit, but a typo like 300 for 30 should stand out).
                    void UpdateScoreIndicator()
                    {
                        if (!DecimalScoreConverter.TryParse(scoreBox.Text, CultureInfo.CurrentCulture, out decimal entered))
                        {
                            scoreBox.BorderBrush = InvalidScoreBrush;
                            ToolTip.SetTip(scoreBox, "Not a valid score — enter a whole number of 0 or more. This isn't saved.");
                        }
                        else if (cell.Status == GradeStatus.Late)
                        {
                            scoreBox.BorderBrush = LateScoreBrush;
                            var penalty = GradeCalculator.CalculateLatePenalty(cell.PointsPossible);
                            var effective = GradeCalculator.CalculateEffectiveScore(
                                new GradeLine(entered, cell.PointsPossible, GradeStatus.Late));
                            var tip =
                                $"Late. Reports subtract {penalty:0.##} points (10% of {cell.PointsPossible:0.##}, rounded up). The {entered:0.##} you entered counts as {effective:0.##}. This box still shows what you entered.";
                            if (entered > cell.PointsPossible)
                            {
                                tip += $" Above the {cell.PointsPossible:0.##} points possible — extra credit?";
                            }

                            ToolTip.SetTip(scoreBox, tip);
                        }
                        else if (cell.IsAboveMax)
                        {
                            scoreBox.BorderBrush = AboveMaxScoreBrush;
                            ToolTip.SetTip(scoreBox, $"Above the {cell.PointsPossible:0.##} points possible — extra credit?");
                        }
                        else
                        {
                            scoreBox.ClearValue(TextBox.BorderBrushProperty);
                            ToolTip.SetTip(scoreBox, null);
                        }
                    }

                    scoreBox.TextChanged += (_, _) => UpdateScoreIndicator();

                    void OnCellPropertyChanged(object? sender, PropertyChangedEventArgs e)
                    {
                        if (e.PropertyName is nameof(GradeCellViewModel.IsAboveMax) or nameof(GradeCellViewModel.Status))
                        {
                            UpdateScoreIndicator();
                        }
                    }

                    // Cleared-out score already becomes 0 in the bound value (via the converter above),
                    // but if it was already 0 that's a no-op change and the Text stays blank on screen —
                    // so on blur, explicitly snap the display back to "0" too. Likewise, invalid text was
                    // never saved, so put back the saved score rather than leave the screen disagreeing with it.
                    scoreBox.LostFocus += (_, _) =>
                    {
                        if (string.IsNullOrWhiteSpace(scoreBox.Text))
                        {
                            scoreBox.Text = "0";
                        }
                        else if (!DecimalScoreConverter.TryParse(scoreBox.Text, CultureInfo.CurrentCulture, out decimal _))
                        {
                            scoreBox.Text = DecimalScoreConverter.Format(cell.Score, CultureInfo.CurrentCulture);
                        }
                    };
                    // Selecting on focus means typing immediately overwrites the value, rather than
                    // requiring the student's whole existing score to be manually cleared first.
                    scoreBox.GotFocus += (_, _) => scoreBox.SelectAll();

                    var statusBox = new ComboBox { ItemsSource = GradeCellViewModel.StatusOptions, Width = CellControlWidth };
                    statusBox.Bind(ComboBox.SelectedItemProperty,
                        new Binding(nameof(GradeCellViewModel.Status)) { Source = cell, Mode = BindingMode.TwoWay });

                    scoreBox.AddHandler(KeyDownEvent, (_, e) => HandleTabKey(e, row, index, isScoreBox: true), RoutingStrategies.Tunnel);
                    statusBox.AddHandler(KeyDownEvent, (_, e) => HandleTabKey(e, row, index, isScoreBox: false), RoutingStrategies.Tunnel);

                    var cellPanel = new StackPanel
                    {
                        Spacing = 2,
                        Margin = new Avalonia.Thickness(2, 4),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Children = { scoreBox, statusBox }
                    };

                    // Hook up to the cell view model and the Tab-order maps only while on screen. These
                    // controls are never recycled, so a cell scrolled out of view gets new controls when it
                    // comes back; without unsubscribing here, the long-lived cell view model would keep
                    // every discarded control alive until the next reload. Switching tabs detaches and later
                    // re-attaches the same controls, so attaching registers everything again.
                    cellPanel.AttachedToVisualTree += (_, _) =>
                    {
                        _scoreBoxes[(row, index)] = scoreBox;
                        _statusBoxes[(row, index)] = statusBox;
                        cell.PropertyChanged += OnCellPropertyChanged;
                        UpdateScoreIndicator();
                    };
                    cellPanel.DetachedFromVisualTree += (_, _) =>
                    {
                        cell.PropertyChanged -= OnCellPropertyChanged;
                        if (_scoreBoxes.TryGetValue((row, index), out var registeredScore) && registeredScore == scoreBox)
                        {
                            _scoreBoxes.Remove((row, index));
                        }

                        if (_statusBoxes.TryGetValue((row, index), out var registeredStatus) && registeredStatus == statusBox)
                        {
                            _statusBoxes.Remove((row, index));
                        }
                    };

                    return cellPanel;
                    // Fresh controls every time (no recycling): each cell binds to a specific GradeCellViewModel
                    // instance via an explicit Source, so a recycled control would show stale data for whatever
                    // row it got reused for. The data scale here (tens of students/assignments) makes this cheap.
                }, supportsRecycling: false)
            });
        }

        // Columns.Clear()/Add() don't trigger the DataGrid's internal frozen-column-state
        // recalculation (Avalonia only recomputes it when FrozenColumnCount itself changes,
        // a column's DisplayIndex/Visible state changes, or in narrow column-insert cases that
        // don't apply here). Toggle it to force Avalonia to re-mark the Student column as frozen.
        var frozenColumnCount = GradeGrid.FrozenColumnCount;
        GradeGrid.FrozenColumnCount = 0;
        GradeGrid.FrozenColumnCount = frozenColumnCount;
    }

    private void HandleTabKey(KeyEventArgs e, GradebookRowViewModel row, int columnIndex, bool isScoreBox)
    {
        if (e.Key != Key.Tab || _subscribedViewModel is not { } viewModel)
        {
            return;
        }

        var forward = !e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var rows = viewModel.Rows;

        if (isScoreBox && forward)
        {
            if (_statusBoxes.TryGetValue((row, columnIndex), out var status))
            {
                e.Handled = true;
                status.Focus();
            }
            return;
        }

        if (!isScoreBox && !forward)
        {
            if (_scoreBoxes.TryGetValue((row, columnIndex), out var score))
            {
                e.Handled = true;
                score.Focus();
            }
            return;
        }

        var rowIndex = rows.IndexOf(row);
        if (rowIndex < 0)
        {
            return;
        }

        if (!isScoreBox) // forward, from a status box: next row's score box, or next column's first row
        {
            if (rowIndex + 1 < rows.Count && _scoreBoxes.TryGetValue((rows[rowIndex + 1], columnIndex), out var nextScore))
            {
                e.Handled = true;
                nextScore.Focus();
            }
            else if (rows.Count > 0 && _scoreBoxes.TryGetValue((rows[0], columnIndex + 1), out var firstOfNextColumn))
            {
                e.Handled = true;
                firstOfNextColumn.Focus();
            }
        }
        else // backward (shift+tab), from a score box: previous row's status box, or previous column's last row
        {
            if (rowIndex - 1 >= 0 && _statusBoxes.TryGetValue((rows[rowIndex - 1], columnIndex), out var prevStatus))
            {
                e.Handled = true;
                prevStatus.Focus();
            }
            else if (rows.Count > 0 && _statusBoxes.TryGetValue((rows[^1], columnIndex - 1), out var lastOfPrevColumn))
            {
                e.Handled = true;
                lastOfPrevColumn.Focus();
            }
        }
    }

    private async Task EditAssignmentAsync(GradebookViewModel viewModel, Assignment assignment)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var (result, name, points, quarter) = await EditAssignmentDialog.ShowAsync(owner, assignment.Name, assignment.PointsPossible, assignment.Quarter);

        switch (result)
        {
            case EditAssignmentDialogResult.Saved:
                await viewModel.UpdateAssignmentAsync(assignment, name, points, quarter);
                break;
            case EditAssignmentDialogResult.Deleted:
                await viewModel.DeleteAssignmentAsync(assignment.Id);
                break;
        }
    }
}
