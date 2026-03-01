using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WalletWasabi.Fluent.Helpers;

public class IndexedItem<T> : INotifyPropertyChanged
{
	private int _index;

	public T Value { get; }

	public int Index
	{
		get => _index;
		internal set { if (_index != value) { _index = value; OnPropertyChanged(); } }
	}

	public IndexedItem(T value, int index)
	{
		Value = value;
		_index = index;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class IndexedCollection<T> : ObservableCollection<IndexedItem<T>>
{
	private readonly ObservableCollection<T> _source;

	public IndexedCollection(ObservableCollection<T> source)
	{
		_source = source;
		_source.CollectionChanged += OnSourceChanged;

		for (int i = 0; i < _source.Count; i++)
		{
			Add(new IndexedItem<T>(_source[i], i));
		}
	}

	private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				for (int i = 0; i < e.NewItems!.Count; i++)
				{
					Insert(e.NewStartingIndex + i,
						new IndexedItem<T>((T)e.NewItems[i]!, e.NewStartingIndex + i));
				}
				ReIndex(e.NewStartingIndex + e.NewItems.Count);
				break;

			case NotifyCollectionChangedAction.Remove:
				for (int i = e.OldItems!.Count - 1; i >= 0; i--)
				{
					RemoveAt(e.OldStartingIndex + i);
				}
				ReIndex(e.OldStartingIndex);
				break;

			case NotifyCollectionChangedAction.Move:
				Move(e.OldStartingIndex, e.NewStartingIndex);
				ReIndex(Math.Min(e.OldStartingIndex, e.NewStartingIndex));
				break;

			case NotifyCollectionChangedAction.Replace:
				for (int i = 0; i < e.NewItems!.Count; i++)
				{
					this[e.NewStartingIndex + i] =
						new IndexedItem<T>((T)e.NewItems[i]!, e.NewStartingIndex + i);
				}
				break;

			case NotifyCollectionChangedAction.Reset:
				Clear();
				for (int i = 0; i < _source.Count; i++)
				{
					Add(new IndexedItem<T>(_source[i], i));
				}
				break;
		}
	}

	private void ReIndex(int startFrom)
	{
		for (int i = startFrom; i < Count; i++)
		{
			this[i].Index = i;
		}
	}
}
