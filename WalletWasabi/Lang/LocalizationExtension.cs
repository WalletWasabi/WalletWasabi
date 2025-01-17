using System.Resources;
using WalletWasabi.Models;

namespace WalletWasabi.Lang;

public static class LocalizationExtension
{
	public static string GetSafeValue(this ResourceManager manager, string key)
	{
		return manager.GetString(key, Resources.Culture) ?? "";
	}

	public static string ToLocalTranslation(this DisplayLanguage language)
	{
		return language switch
		{
			DisplayLanguage.English => "English",
			_ => language.ToString()
		};
	}
}
