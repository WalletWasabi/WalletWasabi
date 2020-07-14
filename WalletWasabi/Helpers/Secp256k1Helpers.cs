using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Helpers
{
	public static class Secp256k1Helpers
	{
		public static bool Equals(GE a, GE b)
			=> a.IsInfinity == b.IsInfinity && (a.x == b.x && a.y == b.y);
	}
}
