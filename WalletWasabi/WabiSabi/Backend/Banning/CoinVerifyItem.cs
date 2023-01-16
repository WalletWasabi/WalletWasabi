using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifyWorkerItem
{
	public CoinVerifyWorkerItem(Coin coin, TaskCompletionSource<CoinVerifyResult> taskCompletionSourceToSet)
	{
		Coin = coin;
		TaskCompletionSourceToSet = taskCompletionSourceToSet;
	}

	public Coin Coin { get; }
	public TaskCompletionSource<CoinVerifyResult> TaskCompletionSourceToSet { get; }
	private TaskCompletionSource<CoinVerifyResult> Result { get; } = new();

	public void Start(CancellationToken token)
	{
	}
}
