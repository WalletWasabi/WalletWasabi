using System;
using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Models
{
	public interface ICoinsView : IEnumerable<SmartCoin>
	{
		ICoinsView AtBlockHeight(Height height);
		ICoinsView Available();
		ICoinsView ChildrenOf(SmartCoin coin);
		ICoinsView CoinJoinInProcess();
		ICoinsView Confirmed();
		ICoinsView DescendantOf(SmartCoin coin);
		ICoinsView FilterBy(Func<SmartCoin, bool> expression);
		ICoinsView OutPoints(IEnumerable<TxoRef> outPoints);
		ICoinsView SpentBy(uint256 txid);
		SmartCoin[] ToArray();
		Money TotalAmount();
		ICoinsView Unconfirmed();
		ICoinsView UnSpent();
		SmartCoin GetByOutPoint(OutPoint outpoint);
	}
}
