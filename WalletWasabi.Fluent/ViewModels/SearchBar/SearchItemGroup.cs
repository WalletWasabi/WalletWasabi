using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchItemGroup : IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<ISearchItem> _items;

	public SearchItemGroup(string title, IObservable<IChangeSet<ISearchItem, ComposedKey>> changes)
	{
		Title = title;
		changes
			.Bind(out _items)
			.DisposeMany()
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public string Title { get; }

	public ReadOnlyObservableCollection<ISearchItem> Items => _items;

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
