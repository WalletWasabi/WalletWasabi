using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.TransactionProcessing
{
	public static class TransactionProcessorExtensions
	{
		public static HdPubKey NewKey(this TransactionProcessor me, string label)
		{
			return me.KeyManager.GenerateNewKey(label, KeyState.Clean, true);
		}
	}
}
