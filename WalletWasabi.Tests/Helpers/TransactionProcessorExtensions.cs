using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;

namespace WalletWasabi.Tests.Helpers;

public static class TransactionProcessorExtensions
{
	public static HdPubKey NewKey(this TransactionProcessor me, string label)
	{
		return me.KeyManager.GenerateNewKey(label, KeyState.Clean, isInternal: true);
	}
}
