using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(Title = "Exclude Coins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ExcludedCoinsViewModel : DialogViewModelBase<Unit>
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private bool _hasSelection;

	public ExcludedCoinsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		var initialCoins = wallet.Coins.List.Items.Where(x => x.IsExcludedFromCoinJoin);
		CoinList = new CoinListViewModel(wallet.Coins, initialCoins.ToList(), allowCoinjoiningCoinSelection: false, ignorePrivacyMode: true);
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = ReactiveCommand.Create(() => Close());
		ToggleSelectionCommand = ReactiveCommand.Create(() => SelectAll(!CoinList.Selection.Any()));
	}

	public CoinListViewModel CoinList { get; set; }

	public ICommand ToggleSelectionCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		CoinList.CoinItems
			.ToObservableChangeSet()
			.WhenPropertyChanged(x => x.IsSelected)
			.Select(_ => CoinList.Selection.Count > 0)
			.BindTo(this, x => x.HasSelection)
			.DisposeWith(disposables);

		CoinList.Selection
			.ToObservableChangeSet()
			.Buffer(TimeSpan.FromMilliseconds(10), RxApp.MainThreadScheduler)
			.DoAsync(async changeSets =>
			{
				var allAdded = changeSets.SelectMany(cs => cs)
					.Where(change => change.Reason == ListChangeReason.Add)
					.Select(change => change.Item.Current)
					.ToArray();

				var allRemoved = changeSets.SelectMany(cs => cs)
					.Where(change => change.Reason == ListChangeReason.Remove)
					.Select(change => change.Item.Current)
					.ToArray();

				if (allAdded.Length > 0 || allRemoved.Length > 0)
				{
					await _wallet.Coins.UpdateExcludedCoinsFromCoinjoinAsync(allAdded, allRemoved);
				}
			})
			.Subscribe()
			.DisposeWith(disposables);

		CoinList.DisposeWith(disposables);
	}

	private void SelectAll(bool value)
	{
		foreach (var coin in CoinList.CoinItems)
		{
			coin.IsSelected = value;
		}
	}
}
