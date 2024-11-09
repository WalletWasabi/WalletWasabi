namespace WalletWasabi.Lang;

public static class Utils
{
	public static string? GetString(string key) =>
		Resources.ResourceManager.GetString(key, Resources.Culture);

	public static string LowerCaseFirst(string word)
	{
		return string.IsNullOrEmpty(word) || char.IsLower(word[0]) ?
			word :
			$"{char.ToLower(word[0], Resources.Culture)}{word[1..]}";
	}

	public static string UpperCaseFirst(string word)
	{
		return string.IsNullOrEmpty(word) || char.IsLower(word[0]) ?
			word :
			$"{char.ToUpper(word[0], Resources.Culture)}{word[1..]}";
	}

	public static string? PluralIfNeeded(int count, string key)
	{
		return count <= 1 ?
			GetString(key) :
			Plural(key);
	}

	public static string? Plural(string key)
	{
		// Specific plural
		if (GetString($"{key}_Plural") is { } plural)
		{
			return plural;
		}

		return GetString(key) is not { } value ?
			null :
			$"{value}{Resources.Utils_GenericPlural}";
	}
}
