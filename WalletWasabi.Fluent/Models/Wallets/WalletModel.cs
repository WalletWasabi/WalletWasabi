using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial interface IWalletModel : INotifyPropertyChanged
{
	bool IsLoggedIn { get; set; }

	bool IsLoaded { get; set; }

	bool IsSelected { get; set; }

	IObservable<bool> IsCoinjoinRunning { get; }

	IObservable<bool> IsCoinjoinStarted { get; }

	bool IsCoinJoinEnabled { get; }

	IAddressesModel Addresses { get; }

	WalletId Id { get; }

	string Name { get; }

	Network Network { get; }

	IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes { get; }

	bool SeveralReceivingScriptTypes { get; }

	IWalletTransactionsModel Transactions { get; }

	IObservable<Amount> Balances { get; }

	IObservable<bool> HasBalance { get; }

	IWalletCoinsModel Coins { get; }

	IWalletAuthModel Auth { get; }

	IWalletLoadWorkflow Loader { get; }

	IWalletSettingsModel Settings { get; }

	IWalletPrivacyModel Privacy { get; }

	IWalletCoinjoinModel? Coinjoin { get; }

	IObservable<bool> Loaded { get; }

	IAmountProvider AmountProvider { get; }

	bool IsHardwareWallet { get; }

	bool IsWatchOnlyWallet { get; }

	IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent);

	IWalletStatsModel GetWalletStats();

	IWalletInfoModel GetWalletInfo();

	IPrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendFlow);

	void Rename(string newWalletName);
}

[AppLifetime]
public partial class WalletModel : ReactiveObject, IWalletModel
{
	private readonly Lazy<WalletCoinjoinModel?> _coinjoin;
	private readonly Lazy<IWalletCoinsModel> _coins;

	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isLoaded;
	[AutoNotify] private bool _isSelected;

	public WalletModel(Wallet wallet, IAmountProvider amountProvider)
	{
		Wallet = wallet;
		AmountProvider = amountProvider;

		Auth = new WalletAuthModel(Wallet);
		Loader = new WalletLoadWorkflow(Wallet);
		Settings = new WalletSettingsModel(Wallet.KeyManager);

		_coinjoin = new(() =>
		{
			var coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();
			return coinJoinManager is not null
				? new WalletCoinjoinModel(Wallet, coinJoinManager, Settings)
				: null;
		});

		_coins = new(() => new WalletCoinsModel(wallet, this));

		Transactions = new WalletTransactionsModel(this, wallet);

		Addresses = new AddressesModel(Wallet);

		Loaded = Services.EventBus.AsObservable<WalletLoaded>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => Wallet.Loaded);

		Privacy = new WalletPrivacyModel(this, Wallet);

		Balances = Transactions.TransactionProcessed
			.Select(_ => Wallet.Coins.TotalAmount())
			.Select(AmountProvider.Create);

		HasBalance = Balances.Select(x => x.HasBalance);

		// Start the Loader after wallet is logged in
		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.Where(x => x)
			.Take(1)
			.Do(_ => Loader.Start())
			.Subscribe();

		// Stop the loader after load is completed
		Loaded.Where(x => x)
			 .Do(_ => Loader.Stop())
			 .Subscribe();

		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.BindTo(this, x => x.IsLoggedIn);

		this.WhenAnyObservable(x => x.Loaded)
			.BindTo(this, x => x.IsLoaded);
	}

	public IObservable<bool> IsCoinjoinRunning => _coinjoin.Value?.IsRunning ?? Observable.Return(false);

	public IObservable<bool> IsCoinjoinStarted => _coinjoin.Value?.IsStarted ?? Observable.Return(false);

	public bool IsCoinJoinEnabled => _coinjoin.Value is not null;

	public IAddressesModel Addresses { get; }

	internal Wallet Wallet { get; }

	public WalletId Id => Wallet.WalletId;

	public string Name => Wallet.WalletName;

	public Network Network => Wallet.Network;

	public IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes => Wallet.KeyManager.AvailableScriptPubKeyTypes;

	public bool SeveralReceivingScriptTypes => AvailableScriptPubKeyTypes.Contains(ScriptPubKeyType.TaprootBIP86);

	public IWalletTransactionsModel Transactions { get; }

	public IObservable<Amount> Balances { get; }

	public IObservable<bool> HasBalance { get; }

	public IWalletCoinsModel Coins => _coins.Value;

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IWalletSettingsModel Settings { get; }

	public WalletPrivacyModel Privacy { get; }

	public WalletCoinjoinModel? Coinjoin => _coinjoin.Value;

	public IObservable<bool> Loaded { get; }

	public IAmountProvider AmountProvider { get; }

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return Wallet.GetLabelsWithRanking(intent);
	}

	public IWalletStatsModel GetWalletStats()
	{
		return new WalletStatsModel(this, Wallet);
	}

	public WalletInfoModel GetWalletInfo()
	{
		return new WalletInfoModel(Wallet);
	}

	public PrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendFlow)
	{
		return new PrivacySuggestionsModel(sendFlow);
	}

	public void Rename(string newWalletName)
	{
		Services.WalletManager.RenameWallet(Wallet, newWalletName);
		this.RaisePropertyChanged(nameof(Name));
	}
}
