using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarWalletListBoxBehavior : DisposingBehavior<ListBox>
{
	private WalletViewModelBase? _closedWallet;

	protected override void OnAttached(CompositeDisposable disposables)
	{
		AssociatedObject?.WhenAnyValue(x => x.Items)
			.Select(x => x as ObservableCollection<WalletViewModelBase>)
			.Where(x => x is { })
			.Do(x => x!.CollectionChanged += CollectionOnCollectionChanged)
			.Subscribe()
			.DisposeWith(disposables);
	}

	private void CollectionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e is { Action: NotifyCollectionChangedAction.Remove, OldItems: [ClosedWalletViewModel closedWallet] })
		{
			_closedWallet = closedWallet;
		}
		else if (_closedWallet is { } && e is
		                              {
			                              Action: NotifyCollectionChangedAction.Add,
			                              NewItems: [WalletViewModel openWallet]
		                              }
		                              && _closedWallet.Title == openWallet.Title)
		{
			AssociatedObject!.SelectedItem = openWallet;
			_closedWallet = null;
		}
		else
		{
			_closedWallet = null;
		}
	}
}
