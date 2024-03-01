using NBitcoin;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets;

public record WalletFactory(
	string DataDir,
	Network Network,
	BitcoinStore BitcoinStore,
	WasabiSynchronizer Synchronizer,
	ServiceConfiguration ServiceConfiguration,
	HybridFeeProvider FeeProvider,
	IBlockProvider BlockProvider)
{
	public Wallet Create(KeyManager keyManager)
	{
		return new(DataDir, Network, keyManager, BitcoinStore, Synchronizer, ServiceConfiguration, FeeProvider, BlockProvider);
	}
}
