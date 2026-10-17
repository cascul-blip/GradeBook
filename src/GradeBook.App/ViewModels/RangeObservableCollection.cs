using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GradeBook.App.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> whose contents can be swapped in one step. Adding items one at
/// a time raises one change per item, and the gradebook rebuilds all of its DataGrid columns on every
/// change to the assignment list; <see cref="ReplaceAll"/> raises a single Reset instead.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
