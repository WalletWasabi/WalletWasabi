using System;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers
{
	public static class TextHelpers
	{
		public static string AddSIfPlural(int n) => n > 1 ? "s" : "";

		public static string CloseSentenceIfZero(params int[] counts) => counts.All(x => x == 0) ? "." : " ";

		private static string ConcatNumberAndUnit(int n, string unit, char closingChar) => n > 0 ? $"{n} {unit}{AddSIfPlural(n)}{closingChar}" : "";

		public static string TimeSpanToFriendlyString(TimeSpan time)
		{
			string result = "";

			result += ConcatNumberAndUnit(time.Days, "day", ' ');
			result += ConcatNumberAndUnit(time.Hours, "hour", ' ');
			result += ConcatNumberAndUnit(time.Minutes, "minute", ' ');

			return result.TrimEnd();
		}
	}
}
