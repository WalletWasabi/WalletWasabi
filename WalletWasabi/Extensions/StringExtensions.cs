namespace System;

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
}
