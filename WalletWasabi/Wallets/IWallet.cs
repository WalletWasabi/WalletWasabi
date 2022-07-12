using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
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

	Task<bool> IsWalletPrivate();
	Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidates(int bestHeight);
}
