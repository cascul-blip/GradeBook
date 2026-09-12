using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using GradeBook.App.Converters;
using GradeBook.App.ViewModels;

namespace GradeBook.App.Views;

public partial class GradebookView : UserControl
{
    private GradebookViewModel? _subscribedViewModel;

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

            GradeGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = $"{assignment.Name} ({assignment.PointsPossible:0.##} pts)",
                Width = new DataGridLength(140),
                CellTemplate = new FuncDataTemplate<GradebookRowViewModel>((row, _) =>
                {
                    if (row is null || index >= row.Cells.Count)
                    {
                        return new TextBlock();
                    }

                    var cell = row.Cells[index];

                    var scoreBox = new TextBox { PlaceholderText = "score" };
                    scoreBox.Bind(TextBox.TextProperty, new Binding(nameof(GradeCellViewModel.Score))
                    {
                        Source = cell,
                        Mode = BindingMode.TwoWay,
                        Converter = DecimalScoreConverter.Instance
                    });
                    // Cleared-out score already becomes 0 in the bound value (via the converter above),
                    // but if it was already 0 that's a no-op change and the Text stays blank on screen —
                    // so on blur, explicitly snap the display back to "0" too.
                    scoreBox.LostFocus += (_, _) =>
                    {
                        if (string.IsNullOrWhiteSpace(scoreBox.Text))
                        {
                            scoreBox.Text = "0";
                        }
                    };

                    var statusBox = new ComboBox { ItemsSource = GradeCellViewModel.StatusOptions };
                    statusBox.Bind(ComboBox.SelectedItemProperty,
                        new Binding(nameof(GradeCellViewModel.Status)) { Source = cell, Mode = BindingMode.TwoWay });

                    return new StackPanel
                    {
                        Spacing = 2,
                        Children = { scoreBox, statusBox }
                    };
                }, supportsRecycling: true)
            });
        }
    }
}
