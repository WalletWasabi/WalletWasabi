using System.Linq;
using WalletWasabi.Helpers;

namespace System
{
	public static class PseudoRandom
	{
		private static Random Random { get; } = new Random();

		public static string GetString(int length, string chars = Constants.CapitalAlphaNumericChars)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);
			Guard.NotNullOrEmpty(nameof(chars), chars);

			var random = new string(Enumerable
				.Repeat(chars, length)
				.Select(s => s[Random.Next(s.Length)])
				.ToArray());
			return random;
		}
	}
}
