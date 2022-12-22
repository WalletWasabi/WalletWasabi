using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Wallets;

public interface IWallet
{
	string WalletName { get; }
	bool IsUnderPlebStop { get; }
	bool IsMixable { get; }

	/// <summary>
	/// Watch only wallets have no key chains.
	/// </summary>
	IKeyChain? KeyChain { get; }

	IDestinationProvider DestinationProvider { get; }
	int AnonymitySetTarget { get; }
	bool ConsolidationMode { get; }
	TimeSpan FeeRateMedianTimeFrame { get; }
	bool RedCoinIsolation { get; }
	bool BatchPayments { get; }

	Task<bool> IsWalletPrivateAsync();

	Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync();

	Task<IEnumerable<SmartTransaction>> GetTransactionsAsync();

	IRoundCoinSelector? GetCoinSelector()
	{
		return null;
	}
}


public interface IRoundCoinSelector
{
	Task<ImmutableList<SmartCoin>> SelectCoinsAsync(IEnumerable<SmartCoin> coinCandidates,
		UtxoSelectionParameters utxoSelectionParameters, Money liquidityClue, SecureRandom secureRandom);
}