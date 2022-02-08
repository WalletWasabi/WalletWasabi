using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public interface ICoinsView : IEnumerable<SmartCoin>
{
	ICoinsView AtBlockHeight(Height height);

	ICoinsView Available();

	ICoinsView ChildrenOf(SmartCoin coin);

	ICoinsView CoinJoinInProcess();

	ICoinsView Confirmed();

	ICoinsView DescendantOf(SmartCoin coin);

	ICoinsView DescendantOfAndSelf(SmartCoin coin);

	ICoinsView FilterBy(Func<SmartCoin, bool> expression);

	ICoinsView OutPoints(ISet<OutPoint> outPoints);

	ICoinsView CreatedBy(uint256 txid);

	ICoinsView SpentBy(uint256 txid);

	SmartCoin[] ToArray();

	Money TotalAmount();

	ICoinsView Unconfirmed();

	ICoinsView Unspent();

	bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin);
}
