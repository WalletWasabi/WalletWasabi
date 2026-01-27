using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Tests.Helpers;

public class StringNoWhiteSpaceEqualityComparer : IEqualityComparer<string?>
{
	public bool Equals(string? x, string? y)
	{
		if (x == y)
		{
			return true;
		}

		if (x is null || y is null)
		{
			return false;
		}

		return Enumerable.SequenceEqual(
			x.Where(c => !char.IsWhiteSpace(c)),
			y.Where(c => !char.IsWhiteSpace(c)));
	}

	public int GetHashCode([DisallowNull] string obj)
	{
		throw new NotImplementedException();
	}
}
