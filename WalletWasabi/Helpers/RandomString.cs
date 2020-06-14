using System.Linq;
using WalletWasabi.Helpers;

namespace System
{
	public static class RandomString
	{
		private static Random Random { get; } = new Random();

		public static string Generate(int length)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);

			return new string(Enumerable.Repeat(Constants.Chars, length)
				.Select(s => s[Random.Next(s.Length)]).ToArray());
		}
	}
}
