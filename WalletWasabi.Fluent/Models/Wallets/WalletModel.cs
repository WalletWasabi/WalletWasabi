using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletModel : ReactiveObject
{
	private readonly Lazy<IWalletCoinjoinModel> _coinjoin;
	private readonly Lazy<IWalletCoinsModel> _coins;
	private readonly ReadOnlyObservableCollection<IAddress> _addresses;

	public WalletModel(Wallet wallet, IAmountProvider amountProvider)
	{
		Wallet = wallet;
		AmountProvider = amountProvider;

		Auth = new WalletAuthModel(this, Wallet);
		Loader = new WalletLoadWorkflow(Wallet);
		Settings = new WalletSettingsModel(Wallet.KeyManager);

		_coinjoin = new(() => new WalletCoinjoinModel(Wallet, Settings));
		_coins = new(() => new WalletCoinsModel(wallet, this));

		Transactions = new WalletTransactionsModel(this, wallet);

		new SignaledFetcher<IAddress, string>(Transactions.TransactionProcessed, x => x.Text, GetAddresses)
			.Changes
			.Bind(out _addresses)
			.Subscribe();

		State =
			Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Select(_ => Wallet.State);

		Privacy = new WalletPrivacyModel(this, Wallet);

		Balances =
			Observable.Defer(() => Observable.Return(Wallet.Coins.TotalAmount()))
					  .Concat(Transactions.TransactionProcessed
					  .Select(_ => Wallet.Coins.TotalAmount()))
					  .Select(AmountProvider.Create);

		HasBalance = Balances.Select(x => x.HasBalance);

		// Start the Loader after wallet is logged in
		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.Where(x => x)
			.Take(1)
			.Do(_ => Loader.Start())
			.Subscribe();

		// Stop the loader after load is completed
		State.Where(x => x == WalletState.Started)
			 .Do(_ => Loader.Stop())
			 .Subscribe();
	}

	internal Wallet Wallet { get; }

	public string Name => Wallet.WalletName;

	public Network Network => Wallet.Network;

	public IWalletTransactionsModel Transactions { get; }

	public IObservable<Amount> Balances { get; }

	public IObservable<bool> HasBalance { get; }

	public IWalletCoinsModel Coins => _coins.Value;

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IWalletSettingsModel Settings { get; }

	public IWalletPrivacyModel Privacy { get; }

	public IWalletCoinjoinModel Coinjoin => _coinjoin.Value;

	public ReadOnlyObservableCollection<IAddress> Addresses => _addresses;

	public IObservable<WalletState> State { get; }

	public IAmountProvider AmountProvider { get; }

	public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
	{
		var pubKey = Wallet.GetNextReceiveAddress(destinationLabels);
		return new Address(Wallet.KeyManager, pubKey);
	}

	public IWalletInfoModel GetWalletInfo()
	{
		return new WalletInfoModel(Wallet);
	}

	public IWalletStatsModel GetWalletStats()
	{
		return new WalletStatsModel(this, Wallet);
	}

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return Wallet.GetLabelsWithRanking(intent);
	}

	private IEnumerable<IAddress> GetAddresses()
	{
		return Wallet.KeyManager
			.GetKeys()
			.Reverse()
			.Select(x => new Address(Wallet.KeyManager, x));
	}
}
