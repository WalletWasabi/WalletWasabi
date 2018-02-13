using DotNetEssentials;
using System.Linq;

namespace System
{
	public static class RandomString
    {
		private static Random Random = new Random();
		public static string Generate(int length)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);

			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			return new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[Random.Next(s.Length)]).ToArray());
		}
	}
}
