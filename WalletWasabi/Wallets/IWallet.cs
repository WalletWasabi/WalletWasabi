using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;

namespace WalletWasabi.Wallets;

public interface IWallet
{
	string WalletName { get; }
	WalletId WalletId { get; }
	Money PlebStopThreshold { get; }
	bool IsMixable { get; }

	/// <summary>
	/// Watch only wallets have no key chains.
	/// </summary>
	IKeyChain? KeyChain { get; }

	IDestinationProvider DestinationProvider { get; }
	OutputProvider OutputProvider => new(DestinationProvider);
	PaymentBatch BatchedPayments => new();

	int AnonScoreTarget { get; }
	bool ConsolidationMode { get; set; }
	TimeSpan FeeRateMedianTimeFrame { get; }
	bool NonPrivateCoinIsolation { get; }

	Task<bool> IsWalletPrivateAsync();

	Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync();

	Task<IEnumerable<SmartTransaction>> GetTransactionsAsync();
}
