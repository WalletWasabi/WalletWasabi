using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Wallets;

public interface IWallet
{
	string Identifier { get; }
	bool CoinjoinEnabled { get; }
	bool IsMixable { get; }
	IKeyChain KeyChain { get; }
	IDestinationProvider DestinationProvider { get; }
	public int AnonScoreTarget { get; }
	public bool ConsolidationMode { get; }
	TimeSpan FeeRateMedianTimeFrame { get; }
	bool RedCoinIsolation { get; }

	Task<bool> IsWalletPrivateAsync();

	Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync(int bestHeight);

	Task<IEnumerable<SmartTransaction>> GetTransactionsAsync();
}
