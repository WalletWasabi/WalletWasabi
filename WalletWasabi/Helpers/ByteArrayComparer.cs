using System.Collections.Generic;

namespace WalletWasabi.Helpers;

public class ByteArrayComparer : IComparer<byte[]>
{
	public static readonly ByteArrayComparer Comparer = new();

	public int Compare(byte[]? x, byte[]? y)
	{
		static int InternalCompare(byte[] left, byte[] right)
		{
			var min = left.Length < right.Length ? left.Length : right.Length;
			for (var i = 0; i < min; i++)
			{
				if (left[i] < right[i])
				{
					return -1;
				}
				if (left[i] > right[i])
				{
					return 1;
				}
			}
			return left.Length.CompareTo(right.Length);
		}

		return (x, y) switch
		{
			(null, null) => 0,
			(null, _) => 1,
			(_, null) => -1,
			({ } left, { } right) => InternalCompare(left, right)
		};
	}
}
