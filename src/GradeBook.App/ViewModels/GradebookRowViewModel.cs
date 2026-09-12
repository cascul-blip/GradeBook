using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GradeBook.App.ViewModels;

public partial class GradebookRowViewModel(int studentId, string studentName) : ObservableObject
{
    public int StudentId { get; } = studentId;
    public string StudentName { get; } = studentName;
    public ObservableCollection<GradeCellViewModel> Cells { get; } = [];
}
