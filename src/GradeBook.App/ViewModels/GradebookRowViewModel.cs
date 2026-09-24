using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GradeBook.App.ViewModels;

public partial class GradebookRowViewModel(string studentName) : ObservableObject
{
    public string StudentName { get; } = studentName;

    /// <summary>One entry per assignment column; null if that student has no grade row for it (shown read-only).</summary>
    public ObservableCollection<GradeCellViewModel?> Cells { get; } = [];
}
