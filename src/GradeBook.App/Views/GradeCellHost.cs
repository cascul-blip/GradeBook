using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GradeBook.App.Converters;
using GradeBook.App.ViewModels;
using GradeBook.Core.GradeCalculation;
using GradeBook.Core.Models;

namespace GradeBook.App.Views;

/// <summary>
/// One gradebook cell. It normally shows the score and status as plain text, and swaps in the real score
/// TextBox and status ComboBox only while the cell is being edited. A grid with many lessons builds a
/// cell for every lesson in every row, and two templated input controls per cell made loading the tab
/// slow; plain text boxes are a small fraction of that cost.
/// </summary>
public sealed class GradeCellHost : Border
{
    // Matches the TextBox/ComboBox default height so the row doesn't change size when a cell starts editing.
    private const double PartHeight = 32;

    private static readonly IBrush InvalidScoreBrush = Brushes.Red;
    private static readonly IBrush AboveMaxScoreBrush = Brushes.Orange;
    private static readonly IBrush LateScoreBrush = Brushes.DodgerBlue;

    private readonly GradeCellViewModel _cell;
    private readonly double _width;
    private readonly Action<KeyEventArgs, bool> _onEditorKeyDown;

    private readonly StackPanel _displayPanel;
    private readonly Border _scoreDisplay;
    private readonly TextBlock _scoreText;
    private readonly TextBlock _statusText;

    private StackPanel? _editorPanel;
    private TextBox? _scoreBox;
    private ComboBox? _statusBox;

    /// <param name="onEditorKeyDown">Tunnelled KeyDown from the score box (true) or status box (false), for Tab navigation.</param>
    public GradeCellHost(GradeCellViewModel cell, double width, Action<KeyEventArgs, bool> onEditorKeyDown)
    {
        _cell = cell;
        _width = width;
        _onEditorKeyDown = onEditorKeyDown;

        Focusable = true;
        Background = Brushes.Transparent;
        Margin = new Avalonia.Thickness(2, 4);
        HorizontalAlignment = HorizontalAlignment.Left;

        _scoreText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _scoreDisplay = new Border
        {
            Width = width,
            Height = PartHeight,
            Padding = new Avalonia.Thickness(10, 0),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Child = _scoreText
        };

        _statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var chevron = new TextBlock { Text = "▾", VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(chevron, Dock.Right);
        var statusDisplay = new Border
        {
            Width = width,
            Height = PartHeight,
            Padding = new Avalonia.Thickness(10, 0, 6, 0),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Child = new DockPanel { Children = { chevron, _statusText } }
        };

        // Same theme colours as the real TextBox/ComboBox (light or dark), so a cell doesn't change look
        // when it starts editing.
        _scoreDisplay.Bind(Border.BackgroundProperty, this.GetResourceObservable("TextControlBackground"));
        statusDisplay.Bind(Border.BackgroundProperty, this.GetResourceObservable("ComboBoxBackground"));
        statusDisplay.Bind(Border.BorderBrushProperty, this.GetResourceObservable("ComboBoxBorderBrush"));

        // Handled so the DataGrid's own cell click handling doesn't pull focus back off the new editor.
        _scoreDisplay.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            FocusScore();
        };
        statusDisplay.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            FocusStatus(openDropDown: true);
        };

        _displayPanel = new StackPanel { Spacing = 2, Children = { _scoreDisplay, statusDisplay } };
        Child = _displayPanel;

        GotFocus += (_, e) =>
        {
            // Focus landing on the cell itself (not one of its editors) means "start editing here".
            if (ReferenceEquals(e.Source, this))
            {
                FocusScore();
            }
        };
        LostFocus += (_, _) => EndEditWhenFocusLeaves();

        AttachedToVisualTree += (_, _) =>
        {
            _cell.PropertyChanged += OnCellPropertyChanged;
            RefreshDisplay();
        };
        DetachedFromVisualTree += (_, _) => _cell.PropertyChanged -= OnCellPropertyChanged;
    }

    private bool IsEditing => Child == _editorPanel && _editorPanel is not null;

    public void FocusScore()
    {
        BeginEdit();
        FocusWhenReady(_scoreBox!);
    }

    public void FocusStatus(bool openDropDown = false)
    {
        BeginEdit();
        FocusWhenReady(_statusBox!);
        if (openDropDown)
        {
            _statusBox!.IsDropDownOpen = true;
        }
    }

    private void BeginEdit()
    {
        if (IsEditing)
        {
            return;
        }

        if (_editorPanel is null)
        {
            CreateEditors();
        }
        else
        {
            // Show the saved score: text left over from the last edit may not match it any more.
            _scoreBox!.Text = DecimalScoreConverter.Format(_cell.Score, CultureInfo.CurrentCulture);
        }

        Child = _editorPanel;
        UpdateEditorIndicator();
    }

    private void EndEditWhenFocusLeaves()
    {
        // Focus moves between this cell's own score box and status box too, so wait until it has settled.
        Dispatcher.UIThread.Post(() =>
        {
            if (IsEditing && !IsKeyboardFocusWithin && _statusBox is not { IsDropDownOpen: true })
            {
                Child = _displayPanel;
                RefreshDisplay();
            }
        }, DispatcherPriority.Input);
    }

    private void CreateEditors()
    {
        var scoreBox = new TextBox { PlaceholderText = "score", Width = _width };
        scoreBox.Bind(TextBox.TextProperty, new Binding(nameof(GradeCellViewModel.Score))
        {
            Source = _cell,
            Mode = BindingMode.TwoWay,
            Converter = DecimalScoreConverter.Instance
        });
        scoreBox.TextChanged += (_, _) => UpdateEditorIndicator();

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
                scoreBox.Text = DecimalScoreConverter.Format(_cell.Score, CultureInfo.CurrentCulture);
            }
        };
        // Selecting on focus means typing immediately overwrites the value, rather than
        // requiring the student's whole existing score to be manually cleared first.
        scoreBox.GotFocus += (_, _) => scoreBox.SelectAll();

        var statusBox = new ComboBox { ItemsSource = GradeCellViewModel.StatusOptions, Width = _width };
        statusBox.Bind(ComboBox.SelectedItemProperty,
            new Binding(nameof(GradeCellViewModel.Status)) { Source = _cell, Mode = BindingMode.TwoWay });
        // Focus can leave while the dropdown is open (that check skips ending the edit), so check again on close.
        statusBox.DropDownClosed += (_, _) => EndEditWhenFocusLeaves();

        scoreBox.AddHandler(KeyDownEvent, (_, e) => _onEditorKeyDown(e, true), RoutingStrategies.Tunnel);
        statusBox.AddHandler(KeyDownEvent, (_, e) => _onEditorKeyDown(e, false), RoutingStrategies.Tunnel);

        _scoreBox = scoreBox;
        _statusBox = statusBox;
        _editorPanel = new StackPanel { Spacing = 2, Children = { scoreBox, statusBox } };
    }

    private static void FocusWhenReady(Control control)
    {
        // A control that was only just put on screen may not be focusable until it has been laid out.
        if (!control.Focus())
        {
            Dispatcher.UIThread.Post(() => control.Focus(), DispatcherPriority.Loaded);
        }
    }

    private void OnCellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GradeCellViewModel.Score) or nameof(GradeCellViewModel.IsAboveMax) or nameof(GradeCellViewModel.Status))
        {
            if (IsEditing)
            {
                UpdateEditorIndicator();
            }
            else
            {
                RefreshDisplay();
            }
        }
    }

    private void RefreshDisplay()
    {
        var scoreText = DecimalScoreConverter.Format(_cell.Score, CultureInfo.CurrentCulture);
        _scoreText.Text = scoreText;
        _statusText.Text = _cell.Status.ToString();

        var (brush, tip) = GetScoreIndicator(scoreText);
        if (brush is null)
        {
            _scoreDisplay.Bind(Border.BorderBrushProperty, this.GetResourceObservable("TextControlBorderBrush"));
        }
        else
        {
            _scoreDisplay.BorderBrush = brush;
        }

        ToolTip.SetTip(_scoreDisplay, tip);
    }

    private void UpdateEditorIndicator()
    {
        if (_scoreBox is null)
        {
            return;
        }

        var (brush, tip) = GetScoreIndicator(_scoreBox.Text);
        if (brush is null)
        {
            _scoreBox.ClearValue(TextBox.BorderBrushProperty);
        }
        else
        {
            _scoreBox.BorderBrush = brush;
        }

        ToolTip.SetTip(_scoreBox, tip);
    }

    /// <summary>
    /// Red border while the text isn't a valid score (it isn't saved). Blue border when the lesson is
    /// late: the box still shows the typed score, and reports subtract 10% of the points possible, rounded
    /// up. Amber border + tooltip when the saved score is above the points possible (allowed as extra
    /// credit, but a typo like 300 for 30 should stand out). Null brush means the default border.
    /// </summary>
    private (IBrush? Brush, string? Tip) GetScoreIndicator(string? text)
    {
        if (!DecimalScoreConverter.TryParse(text, CultureInfo.CurrentCulture, out decimal entered))
        {
            return (InvalidScoreBrush, "Not a valid score — enter a whole number of 0 or more. This isn't saved.");
        }

        if (_cell.Status == GradeStatus.Late)
        {
            var penalty = GradeCalculator.CalculateLatePenalty(_cell.PointsPossible);
            var effective = GradeCalculator.CalculateEffectiveScore(
                new GradeLine(entered, _cell.PointsPossible, GradeStatus.Late));
            var tip =
                $"Late. Reports subtract {penalty:0.##} points (10% of {_cell.PointsPossible:0.##}, rounded up). The {entered:0.##} you entered counts as {effective:0.##}. This box still shows what you entered.";
            if (entered > _cell.PointsPossible)
            {
                tip += $" Above the {_cell.PointsPossible:0.##} points possible — extra credit?";
            }

            return (LateScoreBrush, tip);
        }

        if (_cell.IsAboveMax)
        {
            return (AboveMaxScoreBrush, $"Above the {_cell.PointsPossible:0.##} points possible — extra credit?");
        }

        return (null, null);
    }
}
