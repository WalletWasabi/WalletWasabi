using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.BlockFilters
{
	public class UtxoEntry
	{
		public OutPoint OutPoint { get; }
		public Script Script { get; }
		public string Line { get; }

		public UtxoEntry(OutPoint outPoint, Script script)
		{
			OutPoint = Guard.NotNull(nameof(outPoint), outPoint);
			Script = Guard.NotNull(nameof(script), script);
			Line = $"{outPoint.Hash}:{outPoint.N}:{ByteHelpers.ToHex(script.ToCompressedBytes())}";
		}
	}
}
