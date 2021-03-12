using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers
{
	public static class TextHelpers
	{
		public static string AddSIfPlural(int n) => n > 1 ? "s" : "";

		public static string CloseSentenceIfZero(params int[] counts) => counts.All(x => x == 0) ? "." : " ";

		private static string ConcatNumberAndUnit(int n, string unit) => n > 0 ? $"{n} {unit}{AddSIfPlural(n)}" : "";

		private static void AddIfNotEmpty(List<string> list, string item)
		{
			if (!string.IsNullOrEmpty(item))
			{
				list.Add(item);
			}
		}

		public static string TimeSpanToFriendlyString(TimeSpan time)
		{
			var textMembers = new List<string>();
			string result = "";

			AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Days, "day"));
			AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Hours, "hour"));
			AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Minutes, "minute"));
			AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Seconds, "second"));

			for (int i = 0; i < textMembers.Count; i++)
			{
				result += textMembers[i];

				if (textMembers.Count > 1 && i < textMembers.Count - 2)
				{
					result += ", ";
				}
				else if (textMembers.Count > 1 && i == textMembers.Count - 2)
				{
					result += " and ";
				}
			}

			return result;
		}
	}
}
