using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public interface ICoinsView : IEnumerable<SmartCoin>
{
	ICoinsView Available();

	ICoinsView Confirmed();

	ICoinsView CreatedBy(uint256 txid);

	ICoinsView SpentBy(uint256 txid);

	Money TotalAmount();

	ICoinsView Unconfirmed();

	ICoinsView Unspent();

	bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin);
}
