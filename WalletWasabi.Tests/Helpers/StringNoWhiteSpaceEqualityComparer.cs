using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace WalletWasabi.Tests.Helpers
{
	public class StringNoWhiteSpaceEqualityComparer : IEqualityComparer<string>
	{
		public bool Equals([AllowNull] string x, [AllowNull] string y)
		{
			return Enumerable.SequenceEqual(
				x.Where(c => !char.IsWhiteSpace(c)),
				y.Where(c => !char.IsWhiteSpace(c)));
		}

		public int GetHashCode([DisallowNull] string obj)
		{
			throw new NotImplementedException();
		}
	}
}
