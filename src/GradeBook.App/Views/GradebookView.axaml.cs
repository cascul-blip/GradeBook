using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GradeBook.App.ViewModels;
using GradeBook.Core.Models;

namespace GradeBook.App.Views;

public partial class GradebookView : UserControl
{
    // Wide enough to fit the longest status option ("Uncompleted") plus the ComboBox's dropdown arrow;
    // the score box matches it so the two controls line up visually within a cell.
    private const double CellControlWidth = 120;

    private GradebookViewModel? _subscribedViewModel;

    // Keyed by (row, column index) so Tab/Shift+Tab can jump straight to a specific cell's control
    // without relying on the DataGrid's own row-major cell navigation. Rebuilt on every column rebuild.
    private readonly Dictionary<(GradebookRowViewModel Row, int ColumnIndex), GradeCellHost> _cellHosts = new();

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
        _cellHosts.Clear();

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

                    var host = new GradeCellHost(cell, CellControlWidth,
                        (e, isScoreBox) => HandleTabKey(e, row, index, isScoreBox));

                    // Register for Tab navigation only while on screen, so the map never holds discarded
                    // cells. Switching tabs detaches and later re-attaches the same cell, which registers it again.
                    host.AttachedToVisualTree += (_, _) => _cellHosts[(row, index)] = host;
                    host.DetachedFromVisualTree += (_, _) =>
                    {
                        if (_cellHosts.TryGetValue((row, index), out var registered) && registered == host)
                        {
                            _cellHosts.Remove((row, index));
                        }
                    };

                    return host;
                    // Fresh controls every time (no recycling): each cell binds to a specific GradeCellViewModel
                    // instance via an explicit Source, so a recycled control would show stale data for whatever
                    // row it got reused for. Cells are plain text until edited (see GradeCellHost), so building
                    // one per lesson per row is cheap.
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
            if (_cellHosts.TryGetValue((row, columnIndex), out var host))
            {
                e.Handled = true;
                host.FocusStatus();
            }
            return;
        }

        if (!isScoreBox && !forward)
        {
            if (_cellHosts.TryGetValue((row, columnIndex), out var host))
            {
                e.Handled = true;
                host.FocusScore();
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
            if (rowIndex + 1 < rows.Count && _cellHosts.TryGetValue((rows[rowIndex + 1], columnIndex), out var nextRow))
            {
                e.Handled = true;
                nextRow.FocusScore();
            }
            else if (rows.Count > 0 && _cellHosts.TryGetValue((rows[0], columnIndex + 1), out var firstOfNextColumn))
            {
                e.Handled = true;
                firstOfNextColumn.FocusScore();
            }
        }
        else // backward (shift+tab), from a score box: previous row's status box, or previous column's last row
        {
            if (rowIndex - 1 >= 0 && _cellHosts.TryGetValue((rows[rowIndex - 1], columnIndex), out var previousRow))
            {
                e.Handled = true;
                previousRow.FocusStatus();
            }
            else if (rows.Count > 0 && _cellHosts.TryGetValue((rows[^1], columnIndex - 1), out var lastOfPrevColumn))
            {
                e.Handled = true;
                lastOfPrevColumn.FocusStatus();
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
