using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets;

/// <summary>
/// Class to create <see cref="Wallet"/> instances.
/// </summary>
public record WalletFactory(
	Network Network,
	BitcoinStore BitcoinStore,
	ServiceConfiguration ServiceConfiguration,
	FeeRateEstimationUpdater FeeRateEstimationUpdater,
	BlockProvider BlockProvider,
	EventBus EventBus,
    CpfpInfoProvider? CpfpInfoProvider = null)
{
	public Wallet Create(KeyManager keyManager)
	{
		TransactionProcessor transactionProcessor = new(BitcoinStore.TransactionStore, BitcoinStore.MempoolService, keyManager, ServiceConfiguration.DustThreshold);
		WalletFilterProcessor walletFilterProcessor = new(keyManager, BitcoinStore, transactionProcessor, BlockProvider, EventBus);

		return new(Network, keyManager, BitcoinStore, ServiceConfiguration, FeeRateEstimationUpdater, transactionProcessor, walletFilterProcessor, CpfpInfoProvider);
	}

	public Wallet CreateAndInitialize(KeyManager keyManager)
	{
		Wallet wallet = Create(keyManager);
		wallet.Initialize();

		return wallet;
	}
}
