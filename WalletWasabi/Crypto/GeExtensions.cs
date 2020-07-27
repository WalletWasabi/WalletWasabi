using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin.Secp256k1
{
	public static class GeExtensions
	{
		public static bool IsGenerator(this GE me)
		{
			if (me.IsInfinity)
			{
				return false;
			}
			else if (me.x == EC.G.x && me.y == EC.G.y)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}
