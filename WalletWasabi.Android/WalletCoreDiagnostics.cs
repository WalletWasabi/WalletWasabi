#if (DEBUG && !WASABI_PERSONAL) || WASABI_RELEASE_HARNESS
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Mobile;
using WalletWasabi.Logging;

namespace WalletWasabi.Android;

public sealed partial class WalletInstrumentation
{
	private static async Task DiagnoseSubmissionAsync(RPCClient rpc, string directory, BroadcastReceipt receipt, long approvedFee, CancellationToken token)
	{
		var signed = Transaction.Parse(JournalHex(directory, receipt.TransactionId), Network.RegTest);
		Logger.LogInfo($"Synthetic submission {receipt.TransactionId}: state={receipt.State}, approvedFee={approvedFee}, vsize={signed.GetVirtualSize()}, lockTime={signed.LockTime.Value}, inputs={signed.Inputs.Count}, outputs={signed.Outputs.Count}");
		if (receipt.State != SubmissionState.Uncertain) { return; }
		// Only fixed public regtest fixtures reach this code. Retain policy errors
		// without exporting a wallet, password, journal or signed transaction.
		var acceptance = await rpc.SendCommandAsync("testmempoolaccept", token, new object[] { new[] { signed.ToHex() } });
		Logger.LogInfo($"Synthetic Core acceptance: {acceptance.Result}");
		foreach (var input in signed.Inputs)
		{
			var available = await rpc.GetTxOutAsync(input.PrevOut.Hash, (int)input.PrevOut.N, includeMempool: true, cancellationToken: token);
			var confirmed = await rpc.GetTxOutAsync(input.PrevOut.Hash, (int)input.PrevOut.N, includeMempool: false, cancellationToken: token);
			Logger.LogInfo($"Synthetic input {input.PrevOut}: sequence={input.Sequence.Value}, mempoolAvailable={available is not null}, chainAvailable={confirmed is not null}");
		}
	}
}
#endif
