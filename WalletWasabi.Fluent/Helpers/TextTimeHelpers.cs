using System;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers
{
	public static class TextTimeHelpers
	{
		public static string AddSIfPlural(int n) => n > 1 ? "s" : "";

		public static string CloseSentenceIfZero(params int[] counts) => counts.All(x => x == 0) ? "." : " ";

		private static string ConcatNumberAndUnit(int n, string unit, char closingChar) => n > 0 ? $"{n} {unit}{AddSIfPlural(n)}{closingChar}" : "";

		public static string TimeSpanToFriendlyString(TimeSpan time)
		{
			string result = "";
			var d = time.Days;
			var h = time.Hours;
			var m = time.Minutes;

			result += ConcatNumberAndUnit(d, "day", ' ');
			result += ConcatNumberAndUnit(h, "hour", ' ');
			result += ConcatNumberAndUnit(m, "minute", ' ');

			return result;
		}
	}
}
