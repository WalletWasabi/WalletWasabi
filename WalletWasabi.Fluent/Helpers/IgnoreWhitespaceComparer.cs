using System.Text.RegularExpressions;

namespace WalletWasabi.Fluent.Helpers;

public class IgnoreWhitespaceComparer : StringComparer
{
	static IgnoreWhitespaceComparer()
	{
		Instance = new IgnoreWhitespaceComparer();
	}

	public static IgnoreWhitespaceComparer Instance { get; }

	public override int Compare(string? x, string? y)
	{
		return InvariantCulture.Compare(RemoveWhitespace(x), RemoveWhitespace(y));
	}

	public override bool Equals(string? x, string? y)
	{
		return InvariantCulture.Equals(RemoveWhitespace(x), RemoveWhitespace(y));
	}

	public override int GetHashCode(string obj)
	{
		return RemoveWhitespace(obj)?.GetHashCode() ?? 0;
	}

	private static string? RemoveWhitespace(string? myString)
	{
		return myString is null ? null : Regex.Replace(myString, @"\s+", "");
	}
}
