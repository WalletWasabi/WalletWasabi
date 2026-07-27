using System.Collections.Generic;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;
using WasabiWallet = WalletWasabi.Wallets.Wallet;

namespace WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;

/// <summary>
/// Minimal <see cref="IWalletModel"/> implementation for constructing Fluent view models
/// in unit tests. Only the members the send flow touches are backed; the rest throw.
/// </summary>
public class FakeWalletModel : ReactiveObject, IWalletModel
{
	public FakeWalletModel(WasabiWallet wallet, AmountProvider amountProvider)
	{
		Wallet = wallet;
		AmountProvider = amountProvider;
		Balances = Observable.Return(amountProvider.Create(Money.Coins(1m)));
	}

	public WasabiWallet Wallet { get; }

	public bool IsLoggedIn { get; set; } = true;
	public bool IsLoaded { get; set; } = true;
	public bool IsSelected { get; set; }

	public IObservable<bool> IsCoinjoinRunning => Observable.Return(false);
	public IObservable<bool> IsCoinjoinStarted => Observable.Return(false);
	public bool IsCoinJoinEnabled => false;

	public AddressesModel Addresses => throw new NotSupportedException();
	public WalletId Id { get; } = new(Guid.NewGuid());
	public string Name => "FakeWallet";
	public Network Network => Wallet.Network;
	public IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes => [ScriptPubKeyType.Segwit];
	public bool SeveralReceivingScriptTypes => false;
	public WalletTransactionsModel Transactions => throw new NotSupportedException();
	public IObservable<Amount> Balances { get; }
	public IObservable<bool> HasBalance => Observable.Return(true);
	public WalletCoinsModel Coins => null!;
	public WalletAuthModel Auth => throw new NotSupportedException();
	public WalletLoadWorkflow Loader => throw new NotSupportedException();
	public WalletSettingsModel Settings => throw new NotSupportedException();
	public WalletPrivacyModel Privacy => throw new NotSupportedException();
	public WalletCoinjoinModel? Coinjoin => null;
	public IObservable<bool> Loaded => Observable.Return(true);
	public AmountProvider AmountProvider { get; }
	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;
	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent) => [];

	public IWalletStatsModel GetWalletStats() => throw new NotSupportedException();

	public WalletInfoModel GetWalletInfo() => throw new NotSupportedException();

	public PrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendFlow) => throw new NotSupportedException();

	public void Rename(string newWalletName) => throw new NotSupportedException();
}
