using System;
using System.Collections.Generic;

namespace WalletWasabi.Tests
{
	public static class IListExtensions
	{
		public static void Shuffle<T>(this IList<T> list)
		{
			var rng = new Random();
			var n = list.Count;
			while (n > 1)
			{
				n--;
				var k = rng.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}
	}
}
