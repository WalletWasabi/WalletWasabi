using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public interface ICoinsView : IEnumerable<SmartCoin>
{
	ICoinsView AtBlockHeight(Height height);

	ICoinsView Available();

	ICoinsView Confirmed();

	ICoinsView CreatedBy(uint256 txid);

	ICoinsView SpentBy(uint256 txid);

	Money TotalAmount();

	ICoinsView Unconfirmed();

	ICoinsView Unspent();

	bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin);
}
