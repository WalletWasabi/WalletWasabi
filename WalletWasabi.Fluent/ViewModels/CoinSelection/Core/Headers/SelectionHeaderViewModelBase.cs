using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;

public partial class SelectionHeaderViewModelBase<TKey> : ViewModelBase where TKey : notnull
{
	[AutoNotify] private bool _isSelected;
	private ReadOnlyObservableCollection<ISelectable> _items;

	public SelectionHeaderViewModelBase(IObservable<IChangeSet<ISelectable, TKey>> changeStream, Func<int, string> getContent, IEnumerable<CommandViewModel> commands)
	{
		var collectionChanged = changeStream
			.AutoRefresh(x => x.IsSelected)
			.ToCollection();

		changeStream.Bind(out _items)
			.Subscribe();

		Text = collectionChanged
			.Select(x => getContent(x.Count(y => y.IsSelected)))
			.ReplayLastActive();

		this.WhenAnyValue(x => x.IsSelected)
			.Skip(1)
			.Do(isSelected => _items.ToList().ForEach(x => x.IsSelected = isSelected))
			.Subscribe();

		SelectAllCommand = ReactiveCommand.Create(() => _items.ToList().ForEach(x => x.IsSelected = true));
		SelectNoneCommand = ReactiveCommand.Create(() => _items.ToList().ForEach(x => x.IsSelected = false));

		Commands = commands;
	}

	public IEnumerable<CommandViewModel> Commands { get; }

	public ReactiveCommand<Unit, Unit> SelectNoneCommand { get; }

	public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }

	public IObservable<string> Text { get; }
}

public class CommandViewModel
{
	public string Header { get; }
	public ReactiveCommand<Unit, Unit> Command { get; }

	public CommandViewModel(string header, ReactiveCommand<Unit, Unit> command)
	{
		Header = header;
		Command = command;
	}
}
