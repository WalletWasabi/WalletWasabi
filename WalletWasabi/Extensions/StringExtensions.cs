using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WalletWasabi.Extensions;

public static class StringExtensions
{
	/// <summary>
	/// Removes one leading occurrence of the specified string
	/// </summary>
	public static string TrimStart(this string me, string trimString, StringComparison comparisonType)
	{
		if (me.StartsWith(trimString, comparisonType))
		{
			return me[trimString.Length..];
		}
		return me;
	}

	/// <summary>
	/// Removes one trailing occurrence of the specified string
	/// </summary>
	public static string TrimEnd(this string me, string trimString, StringComparison comparisonType)
	{
		if (me.EndsWith(trimString, comparisonType))
		{
			return me[..^trimString.Length];
		}
		return me;
	}

	/// <summary>
	/// Returns true if the string contains leading or trailing whitespace, otherwise returns false.
	/// </summary>
	public static bool IsTrimmable(this string me)
	{
		if (me.Length == 0)
		{
			return false;
		}

		return char.IsWhiteSpace(me[0]) || char.IsWhiteSpace(me[^1]);
	}

	public static string[] SplitWords(this string text) =>
		text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

	public static string[] SplitLines(this string text, int lineWidth)
	{
		static void InternalSplit(string text, int lineWidth, List<string> result)
		{
			while (true)
			{
				if (text.Length < lineWidth)
				{
					result.Add(text);
					return;
				}

				var line = text.SplitWords()
					.Scan(string.Empty, (l, w) => l + w + ' ')
					.TakeWhile(l => l.Length <= lineWidth)
					.DefaultIfEmpty(text)
					.Last();
				result.Add(line);
				text = text[(line.Length)..];
			}
		}

		List<string> result = new();
		InternalSplit(text, lineWidth, result);
		return result.ToArray();
	}

	/// <summary>
	/// Counts the occurrences of subText inside of text using Regex.Match()
	/// </summary>
	public static int CountOccurrencesOf(this string text, string subText)
	{
		return Regex.Matches(text, Regex.Escape(subText)).Count;
	}
}
