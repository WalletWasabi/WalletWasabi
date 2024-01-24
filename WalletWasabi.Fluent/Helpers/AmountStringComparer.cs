using System.Text.RegularExpressions;

namespace WalletWasabi.Fluent.Helpers;

public class AmountStringComparer : StringComparer
{
	static AmountStringComparer()
	{
		Instance = new AmountStringComparer();
	}

	public static AmountStringComparer Instance { get; }

	public override int Compare(string? x, string? y)
	{
		return CompareCore(x, y);
	}

	public override bool Equals(string? x, string? y)
	{
		return CompareCore(x, y) == 0;
	}

	public override int GetHashCode(string obj)
	{
		return 0;
	}

	private static string? RemoveWhitespace(string? myString)
	{
		return myString is null ? null : Regex.Replace(myString, @"\s+", "");
	}

	private static int CompareCore(string? x, string? y)
	{
		if (decimal.TryParse(RemoveWhitespace(x), out var a) && decimal.TryParse(RemoveWhitespace(y), out var b))
		{
			return decimal.Compare(a, b);
		}

		return InvariantCulture.Compare(x, y);
	}
}
