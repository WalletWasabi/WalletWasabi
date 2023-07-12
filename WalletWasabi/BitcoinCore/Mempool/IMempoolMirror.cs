using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.BitcoinCore.Mempool;
public interface IMempoolMirror
{
	public event EventHandler<TimeSpan>? Tick;
	internal ISet<uint256> GetMempoolHashes();
}
