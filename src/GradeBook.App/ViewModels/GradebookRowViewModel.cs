using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GradeBook.App.ViewModels;

public partial class GradebookRowViewModel(string studentName) : ObservableObject
{
    public string StudentName { get; } = studentName;
    public ObservableCollection<GradeCellViewModel> Cells { get; } = [];
}
