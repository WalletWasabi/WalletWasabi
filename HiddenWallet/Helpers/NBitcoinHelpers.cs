using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HiddenWallet.Helpers
{
    public static class NBitcoinHelpers
    {
		public static string HashOutpoints(IEnumerable<OutPoint> outPoints)
		{
			StringBuilder sb = new StringBuilder();
			foreach (OutPoint input in outPoints.OrderBy(x => x.Hash.ToString()).OrderBy(x => x.N))
			{
				sb.Append(input.ToHex());
			}

			return HashHelpers.GenerateShortSha1Hash(sb.ToString());
		}

	}
}
