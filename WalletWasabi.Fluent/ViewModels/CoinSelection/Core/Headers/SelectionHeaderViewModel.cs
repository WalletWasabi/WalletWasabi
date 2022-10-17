using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;

public partial class SelectionHeaderViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<ISelectable> _selectables;
	[AutoNotify] private bool _isSelected;

	public SelectionHeaderViewModel(
		IObservable<IChangeSet<ISelectable, OutPoint>> changeStream,
		Func<int, string> getContent,
		IEnumerable<CommandViewModel> commands)
	{
		changeStream
			.Bind(out _selectables)
			.Subscribe()
			.DisposeWith(_disposables);

		Text = changeStream
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Select(x => getContent(x.Count(y => y.IsSelected)));

		Commands = commands;
	}

	public IEnumerable<CommandViewModel> Commands { get; }

	public IObservable<string> Text { get; }

	public ReadOnlyObservableCollection<ISelectable> Selectables => _selectables;

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
