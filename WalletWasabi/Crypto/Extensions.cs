using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace System.Linq
{
	public static class Extensions
	{
		public static GroupElement Sum(this IEnumerable<GroupElement> groupElements) =>
			groupElements.Aggregate(GroupElement.Infinity, (ge, acc) => ge + acc);
	}
}
