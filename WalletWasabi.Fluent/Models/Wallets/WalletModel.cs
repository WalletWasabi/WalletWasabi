using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.Batching;
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

	AddressesModel Addresses { get; }

	WalletId Id { get; }

	string Name { get; }

	Network Network { get; }

	IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes { get; }

	bool SeveralReceivingScriptTypes { get; }

	WalletTransactionsModel Transactions { get; }

	IObservable<Amount> Balances { get; }

	IObservable<bool> HasBalance { get; }

	WalletCoinsModel Coins { get; }

	WalletAuthModel Auth { get; }

	WalletLoadWorkflow Loader { get; }

	WalletSettingsModel Settings { get; }

	WalletPrivacyModel Privacy { get; }

	WalletCoinjoinModel? Coinjoin { get; }

	IObservable<bool> Loaded { get; }

	AmountProvider AmountProvider { get; }

	bool IsHardwareWallet { get; }

	bool IsWatchOnlyWallet { get; }

	IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent);

	IWalletStatsModel GetWalletStats();

	WalletInfoModel GetWalletInfo();

	PrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendFlow);

	void Rename(string newWalletName);

	void UpdateUsedHdPubKeysLabels(Dictionary<HdPubKey, LabelsArray> hdPubKeysWithNewLabels);

	/// <summary>
	/// Checks if a transaction can be built with the given coins and transaction info.
	/// Used by LabelSelectionViewModel for pocket selection validation.
	/// </summary>
	bool CanBuildTransaction(TransactionInfo info, IEnumerable<SmartCoin> coins, IEnumerable<SmartCoin> allCoins);

	/// <summary>
	/// Builds a transaction with the given transaction info.
	/// </summary>
	BuildTransactionResult BuildTransaction(TransactionInfo info, bool tryToSign = true);

	/// <summary>
	/// Builds a changeless transaction (no change output).
	/// </summary>
	BuildTransactionResult BuildChangelessTransaction(
		Destination destination,
		LabelsArray recipient,
		FeeRate feeRate,
		IEnumerable<SmartCoin> coins,
		bool tryToSign = true);

	/// <summary>
	/// Builds a transaction with explicit coin selection.
	/// </summary>
	BuildTransactionResult BuildTransaction(
		Destination destination,
		Money amount,
		LabelsArray recipient,
		FeeRate feeRate,
		IEnumerable<SmartCoin> coins,
		bool subtractFee,
		bool tryToSign = true);

	/// <summary>
	/// Tries to get the maximum fee rate for a transaction.
	/// </summary>
	FeeRate? TryGetMaxFeeRate(TransactionInfo info);

	/// <summary>
	/// Gets fee rate estimations for the wallet's network.
	/// </summary>
	FeeRateEstimations? FeeRateEstimations { get; }

	// Coin operations for SendFlowModel
	int AnonScoreTarget { get; }
	ICoinsView GetCoinsView();
	Pocket[] GetPockets();

	// Batched payment operations
	bool AreTherePendingPayments { get; }
	IReadOnlyList<Payment> GetBatchedPayments();
	void CancelCoinJoinPayment(Guid paymentId);
	void AddCoinJoinPayment(IDestination destination, Money amount);
}

[AppLifetime]
public partial class WalletModel : ReactiveObject, IWalletModel
{
	private readonly IServices _services;
	private readonly Lazy<WalletCoinjoinModel?> _coinjoin;
	private readonly Lazy<WalletCoinsModel> _coins;

	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private bool _isLoaded;
	[AutoNotify] private bool _isSelected;

	public WalletModel(IServices services, Wallet wallet, AmountProvider amountProvider)
	{
		_services = services;
		Wallet = wallet;
		AmountProvider = amountProvider;

		Auth = new WalletAuthModel(services, Wallet.WalletId);
		Loader = new WalletLoadWorkflow(services, this);
		Settings = new WalletSettingsModel(services, Wallet.KeyManager);

		_coinjoin = new(() =>
		{
			return services.IsCoinJoinEnabled()
				? new WalletCoinjoinModel(services, this, Settings)
				: null;
		});

		_coins = new(() => new WalletCoinsModel(this, services));

		Transactions = new WalletTransactionsModel(services, this);

		Addresses = new AddressesModel(services, this);

		Loaded = services.SubscribeToEvent<WalletLoaded>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => Wallet.Loaded);

		Privacy = new WalletPrivacyModel(this);

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

	public AddressesModel Addresses { get; }

	internal Wallet Wallet { get; }

	public WalletId Id => Wallet.WalletId;

	public string Name => Wallet.WalletName;

	public Network Network => Wallet.Network;

	public IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes => Wallet.KeyManager.AvailableScriptPubKeyTypes;

	public bool SeveralReceivingScriptTypes => AvailableScriptPubKeyTypes.Contains(ScriptPubKeyType.TaprootBIP86);

	public WalletTransactionsModel Transactions { get; }

	public IObservable<Amount> Balances { get; }

	public IObservable<bool> HasBalance { get; }

	public WalletCoinsModel Coins => _coins.Value;

	public WalletAuthModel Auth { get; }

	public WalletLoadWorkflow Loader { get; }

	public WalletSettingsModel Settings { get; }

	public WalletPrivacyModel Privacy { get; }

	public WalletCoinjoinModel? Coinjoin => _coinjoin.Value;

	public IObservable<bool> Loaded { get; }

	public AmountProvider AmountProvider { get; }

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return _services.GetLabelsWithRanking(Id, intent);
	}

	public IWalletStatsModel GetWalletStats()
	{
		return new WalletStatsModel(this);
	}

	public WalletInfoModel GetWalletInfo()
	{
		return new WalletInfoModel(this);
	}

	public PrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendFlow)
	{
		return new PrivacySuggestionsModel(_services, sendFlow);
	}

	public void Rename(string newWalletName)
	{
		_services.RenameWallet(Wallet.WalletName, newWalletName);
		this.RaisePropertyChanged(nameof(Name));
	}

	public void UpdateUsedHdPubKeysLabels(Dictionary<HdPubKey, LabelsArray> hdPubKeysWithNewLabels)
	{
		Wallet.UpdateUsedHdPubKeysLabels(hdPubKeysWithNewLabels);
	}

	public bool CanBuildTransaction(TransactionInfo info, IEnumerable<SmartCoin> coins, IEnumerable<SmartCoin> allCoins)
	{
		return TransactionHelpers.TryBuildTransactionWithoutPrevTx(
			Wallet.KeyManager,
			info,
			new CoinsView(allCoins),
			coins,
			Wallet.Password,
			out _);
	}

	public BuildTransactionResult BuildTransaction(TransactionInfo info, bool tryToSign = true)
	{
		return TransactionHelpers.BuildTransaction(Wallet, info, tryToSign);
	}

	public BuildTransactionResult BuildChangelessTransaction(
		Destination destination,
		LabelsArray recipient,
		FeeRate feeRate,
		IEnumerable<SmartCoin> coins,
		bool tryToSign = true)
	{
		return Wallet.BuildChangelessTransaction(destination, recipient, feeRate, coins, tryToSign: tryToSign);
	}

	public BuildTransactionResult BuildTransaction(
		Destination destination,
		Money amount,
		LabelsArray recipient,
		FeeRate feeRate,
		IEnumerable<SmartCoin> coins,
		bool subtractFee,
		bool tryToSign = true)
	{
		return Wallet.BuildTransaction(destination, amount, recipient, feeRate, coins, subtractFee, tryToSign: tryToSign);
	}

	public FeeRate? TryGetMaxFeeRate(TransactionInfo info)
	{
		FeeRate? rate;
		if (info.IsPayToMany)
		{
			var intent = TransactionHelpers.BuildPayToManyIntent(info);
			FeeHelpers.TryGetMaxFeeRateForPayToMany(Wallet, intent, info.FeeRate, info.Coins, out rate);
		}
		else
		{
			FeeHelpers.TryGetMaxFeeRate(Wallet, info.Destination, info.Amount, info.Recipient, info.FeeRate, info.Coins, info.SubtractFee, out rate);
		}
		return rate;
	}

	public FeeRateEstimations? FeeRateEstimations => Wallet.FeeRateEstimations;

	// Coin operations for SendFlowModel
	public int AnonScoreTarget => Wallet.AnonScoreTarget;

	public ICoinsView GetCoinsView() => Wallet.Coins;

	public Pocket[] GetPockets() => Wallet.GetPockets().ToArray();

	// Batched payment operations
	public bool AreTherePendingPayments => Wallet.BatchedPayments.AreTherePendingPayments;

	public IReadOnlyList<Payment> GetBatchedPayments() => Wallet.BatchedPayments.GetPayments();

	public void CancelCoinJoinPayment(Guid paymentId) => Wallet.CancelCoinJoinPayment(paymentId);

	public void AddCoinJoinPayment(IDestination destination, Money amount) => Wallet.AddCoinJoinPayment(destination, amount);
}
