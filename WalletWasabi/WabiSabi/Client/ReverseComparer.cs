using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Client;

public class ReverseComparer : IComparer<long>
{
	public static readonly ReverseComparer Default = new();

	public int Compare(long x, long y)
	{
		// Compare y and x in reverse order.
		return y.CompareTo(x);
	}
}
