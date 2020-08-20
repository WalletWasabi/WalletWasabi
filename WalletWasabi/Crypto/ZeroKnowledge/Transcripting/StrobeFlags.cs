using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.ZeroKnowledge.Transcripting
{
	[Flags]
	public enum StrobeFlags
	{
		I = 1,
		A = 2,
		C = 4,
		T = 8,
		M = 16,
		K = 32
	}
}
