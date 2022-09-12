using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;

public partial class SelectionHeaderViewModelBase<TKey> : ViewModelBase, IDisposable where TKey : notnull
{
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private bool _isSelected;

	public SelectionHeaderViewModelBase(
		IObservable<IChangeSet<ISelectable, TKey>> changeStream,
		Func<int, string> getContent,
		IEnumerable<CommandViewModel> commands)
	{
		changeStream.Bind(out var items)
			.Subscribe()
			.DisposeWith(_disposables);

		Text = changeStream
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Select(x => getContent(x.Count(y => y.IsSelected)))
			.ReplayLastActive();

		this.WhenAnyValue(x => x.IsSelected)
			.Skip(1)
			.Do(isSelected => items.ToList().ForEach(x => x.IsSelected = isSelected))
			.Subscribe()
			.DisposeWith(_disposables);

		Commands = commands;
	}

	public IEnumerable<CommandViewModel> Commands { get; }

	public IObservable<string> Text { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
