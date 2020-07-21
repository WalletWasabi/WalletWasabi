using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin.Secp256k1
{
	public static class Secp256k1Helpers
	{
		public static bool Equals(GE a, GE b)
		{
			if (a.IsInfinity && b.IsInfinity)
			{
				return true;
			}
			else
			{
				return a.IsInfinity == b.IsInfinity && a.x == b.x && a.y == b.y;
			}
		}

		public static bool Equals(GE a, GEJ b) => Equals(a, b.ToGroupElement());

		public static bool Equals(GEJ a, GE b) => Equals(a.ToGroupElement(), b);

		public static bool Equals(GEJ a, GEJ b) => Equals(a.ToGroupElement(), b.ToGroupElement());
	}
}
