using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Mogri.Helpers;

/// <summary>
/// An observable collection that publishes a range insertion as one collection change.
/// </summary>
public sealed class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemsToAdd = items.ToList();
        if (itemsToAdd.Count == 0)
        {
            return;
        }

        var startIndex = Count;

        foreach (var item in itemsToAdd)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            itemsToAdd,
            startIndex));
    }
}