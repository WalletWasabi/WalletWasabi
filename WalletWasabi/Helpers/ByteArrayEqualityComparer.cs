using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Helpers;

public class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
{
	public bool Equals([AllowNull] byte[] x, [AllowNull] byte[] y) => ByteHelpers.CompareFastUnsafe(x, y);

	public int GetHashCode([DisallowNull] byte[] obj)
	{
		var hashcode = new HashCode();
		hashcode.AddBytes(obj);
		return hashcode.ToHashCode();
	}
}
