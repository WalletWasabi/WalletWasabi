using System.Linq;
using WalletWasabi.Helpers;

namespace System
{
	public static class PseudoRandom
	{
		public const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		private static Random Random { get; } = new Random();

		public static string GetString(int length, string chars = Chars)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);

			var random = new string(Enumerable
				.Repeat(chars, length)
				.Select(s => s[Random.Next(s.Length)])
				.ToArray());
			return random;
		}
	}
}
