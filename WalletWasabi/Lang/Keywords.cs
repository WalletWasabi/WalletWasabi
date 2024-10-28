using System.Collections.Generic;
using System.Globalization;

namespace WalletWasabi.Lang;

public static class Keywords
{
	public static string[] ConstructKeywords(string keywordsKey)
	{
		var englishCulture = CultureInfo.GetCultureInfo("en-US");
		if (Resources.ResourceManager.GetString(keywordsKey, englishCulture) is not { } keywords)
		{
			return [];
		}

		var keywordsEnglish = keywords.Replace(" ", "").Split(',');

		if (Resources.Culture.Equals(englishCulture))
		{
			return keywordsEnglish;
		}

		// Use both english keywords and keywords of the current culture
		List<string> result = [];
		foreach (var keywordEnglish in keywordsEnglish)
		{
			result.Add(keywordEnglish);
			if (Resources.ResourceManager.GetString($"Words_{keywordEnglish}", Resources.Culture) is { } keywordCurrentCulture &&
			    !result.Contains(keywordCurrentCulture))
			{
				result.Add(keywordCurrentCulture);
			}
		}

		return result.ToArray();
	}
}
