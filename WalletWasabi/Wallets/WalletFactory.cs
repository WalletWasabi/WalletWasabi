using NBitcoin;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets;

/// <summary>
/// Class to create <see cref="Wallet"/> instances.
/// </summary>
public record WalletFactory(
	string DataDir,
	Network Network,
	BitcoinStore BitcoinStore,
	WasabiSynchronizer WasabiSynchronizer,
	ServiceConfiguration ServiceConfiguration,
	HybridFeeProvider FeeProvider,
	IBlockProvider BlockProvider)
{
	public Wallet Create(KeyManager keyManager)
	{
		TransactionProcessor transactionProcessor = new(BitcoinStore.TransactionStore, BitcoinStore.MempoolService, keyManager, ServiceConfiguration.DustThreshold);
		WalletFilterProcessor walletFilterProcessor = new(keyManager, BitcoinStore, transactionProcessor, BlockProvider);

		return new(DataDir, Network, keyManager, BitcoinStore, WasabiSynchronizer, ServiceConfiguration, FeeProvider, transactionProcessor, walletFilterProcessor);
	}

	public Wallet CreateAndInitialize(KeyManager keyManager)
	{
		Wallet wallet = Create(keyManager);
		wallet.Initialize();

		return wallet;
	}
}
