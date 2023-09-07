using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Logging;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;
using Logger = WalletWasabi.Logging.Logger;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletModel : ReactiveObject
{
	private readonly TransactionHistoryBuilder _historyBuilder;
	private readonly Lazy<IWalletCoinjoinModel> _coinjoin;
	private string _name;
	private string _path;

	public WalletModel(Wallet wallet)
	{
		Wallet = wallet;

		_historyBuilder = new TransactionHistoryBuilder(Wallet);

		Auth = new WalletAuthModel(this, Wallet);
		Loader = new WalletLoadWorkflow(Wallet);
		Settings = new WalletSettingsModel(Wallet.KeyManager);

		_coinjoin = new(() => new WalletCoinjoinModel(Wallet, Settings));

		var relevantTransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(Wallet, nameof(Wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(Wallet, nameof(Wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);

		Coins =
			Observable.Defer(() => GetCoins().ToObservable())                                                 // initial coin list
					  .Concat(relevantTransactionProcessed.SelectMany(_ => GetCoins()))                       // Refresh whenever there's a relevant transaction
					  .Concat(this.WhenAnyValue(x => x.Settings.AnonScoreTarget).SelectMany(_ => GetCoins())) // Also refresh whenever AnonScoreTarget changes
					  .ToObservableChangeSet();

		Transactions =
			Observable.Defer(() => BuildSummary().ToObservable())
					  .Concat(relevantTransactionProcessed.SelectMany(_ => BuildSummary()))
					  .ToObservableChangeSet(x => x.TransactionId);

		Addresses =
			Observable.Defer(() => GetAddresses().ToObservable())
					  .Concat(relevantTransactionProcessed.ToSignal().SelectMany(_ => GetAddresses()))
					  .ToObservableChangeSet(x => x.Text);

		State =
			Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Select(_ => Wallet.State);

		Privacy = new WalletPrivacyModel(this, Wallet);

		var balance =
			Observable.Defer(() => Observable.Return(Wallet.Coins.TotalAmount()))
					  .Concat(relevantTransactionProcessed.Select(_ => Wallet.Coins.TotalAmount()));
		Balances = new WalletBalancesModel(balance, new ExchangeRateProvider(wallet.Synchronizer));

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

		_path = $"WalletMetadata/{Wallet.WalletName}.json";
	}

	internal Wallet Wallet { get; }

	public IWalletBalancesModel Balances { get; }

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IWalletSettingsModel Settings { get; }

	public IWalletPrivacyModel Privacy { get; }

	public IWalletCoinjoinModel Coinjoin => _coinjoin.Value;

	public IObservable<IChangeSet<ICoinModel>> Coins { get; }

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	public string Name
	{
		get => GetName();
		set
		{
			SetName(value);
			this.RaisePropertyChanged();
		}
	}

	public string Id => Wallet.WalletName;

	private void SetName(string name)
	{
		try
		{
			var directoryName = Path.GetDirectoryName(_path);

			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName!);
			}

			using var fileStream = File.Create(_path);
			JsonSerializer.Serialize(fileStream, new WalletMetadata() { Name = name });
		}
		catch (Exception e)
		{
			Logger.LogWarning($"Cannot save Wallet Name {Wallet}: {e.Message}");
		}
	}

	private string GetName()
	{
		if (!File.Exists(_path))
		{
			return Wallet.WalletName;
		}

		try
		{
			using var fileStream = File.OpenRead(_path);
			var walletMetadata = JsonSerializer.Deserialize<WalletMetadata>(fileStream);
			return walletMetadata.Name;
		}
		catch
		{
			return Wallet.WalletName;
		}
	}

	public IObservable<WalletState> State { get; }

	public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

	public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
	{
		var pubKey = Wallet.GetNextReceiveAddress(destinationLabels);
		return new Address(Wallet.KeyManager, pubKey);
	}

	public IWalletInfoModel GetWalletInfo()
	{
		return new WalletInfoModel(Wallet);
	}

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return Wallet.GetLabelsWithRanking(intent);
	}

	private IEnumerable<ICoinModel> GetCoins()
	{
		return Wallet.Coins.Select(x => new CoinModel(Wallet, x));
	}

	private IEnumerable<TransactionSummary> BuildSummary()
	{
		return _historyBuilder.BuildHistorySummary();
	}

	private IEnumerable<IAddress> GetAddresses()
	{
		return Wallet.KeyManager
			.GetKeys()
			.Reverse()
			.Select(x => new Address(Wallet.KeyManager, x));
	}
}
