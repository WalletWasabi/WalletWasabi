using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Helpers;

public static partial class TextHelpers
{
	public static string CloseSentenceIfZero(params int[] counts) => counts.All(x => x == 0) ? "." : " ";

	private static string ConcatNumberAndUnit(int n, string unit) => n > 0 ? $"{n} {unit}" : "";

	[GeneratedRegex(@"\s+")]
	private static partial Regex ParseLabelRegex();

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

		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Days, Lang.Utils.LowerCaseFirst(Lang.Utils.PluralIfNeeded(time.Days, "Words_Day")!)));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Hours, Lang.Utils.LowerCaseFirst(Lang.Utils.PluralIfNeeded(time.Hours, "Words_Hour")!)));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Minutes, Lang.Utils.LowerCaseFirst(Lang.Utils.PluralIfNeeded(time.Minutes, "Words_Minute")!)));
		AddIfNotEmpty(textMembers, ConcatNumberAndUnit(time.Seconds, Lang.Utils.LowerCaseFirst(Lang.Utils.PluralIfNeeded(time.Seconds, "Words_Second")!)));

		for (int i = 0; i < textMembers.Count; i++)
		{
			result += textMembers[i];

			if (textMembers.Count > 1 && i < textMembers.Count - 2)
			{
				result += ", ";
			}
			else if (textMembers.Count > 1 && i == textMembers.Count - 2)
			{
				result += $" {Lang.Utils.LowerCaseFirst(Lang.Resources.Words_And)} ";
			}
		}

		return result;
	}

	public static string ToBtcWithUnit(this Money money)
	{
		return money.ToFormattedString();
	}

	public static string ToFormattedString(this Money money)
	{
		const int WholeGroupSize = 3;

		var moneyString = money.ToString(fplus: false, false);

		moneyString = moneyString.Insert(moneyString.Length - 3, " ");
		moneyString = moneyString.Insert(moneyString.Length - 7, " ");

		var startIndex = moneyString.IndexOf('.') - WholeGroupSize;

		if (startIndex > 0)
		{
			for (var i = startIndex; i > 0; i -= WholeGroupSize)
			{
				moneyString = moneyString.Insert(i, " ");
			}
		}

		return moneyString;
	}

	public static string ParseLabel(this string text) => ParseLabelRegex().Replace(text, " ").Trim();

	public static string TotalTrim(this string text)
	{
		return text
			.Replace("\r", "")
			.Replace("\n", "")
			.Replace("\t", "")
			.Replace(" ", "");
	}

	public static string GetConfirmationText(int confirmations)
	{
		return $"{Lang.Resources.Words_Confirmed} ({confirmations} {Lang.Utils.LowerCaseFirst(Lang.Utils.PluralIfNeeded(confirmations, "Words_confirmation")!)})";
	}

	public static string FormatPercentageDiff(double n)
	{
		var precision = 0.01m;
		var withFriendlyDecimals = (n * 100).WithFriendlyDecimals();

		if (Math.Abs(withFriendlyDecimals) < precision)
		{
			var threshold = n > 0 ? "+" + precision : "-" + precision;
			return $"{Lang.Resources.Sentences_less_than} " + threshold.ToString(CultureInfo.InvariantCulture) + "%";
		}
		else
		{
			var diffPart = withFriendlyDecimals.ToString(CultureInfo.InvariantCulture);
			var numericPart = n > 0 ? "+" + diffPart : diffPart;
			return numericPart + "%";
		}
	}
}
