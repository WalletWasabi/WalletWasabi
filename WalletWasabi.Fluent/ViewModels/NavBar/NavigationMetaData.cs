using WalletWasabi.Fluent.Models;

#pragma warning disable IDE0130 // Namespace does not match folder structure (see https://github.com/WalletWasabi/WalletWasabi/pull/10576#issuecomment-1552750543)

namespace WalletWasabi.Fluent;

public sealed record NavigationMetaData(
	bool Searchable = true,
	bool IsLocalized = false,
	string? Title = null,
	string? Caption = null,
	string? IconName = null,
	string? IconNameFocused = null,
	int Order = 0,
	SearchCategory Category = SearchCategory.None,
	string? Keywords = null,
	NavBarPosition NavBarPosition = default,
	NavBarSelectionMode NavBarSelectionMode = default,
	NavigationTarget NavigationTarget = default
)
{
	public string[]? GetKeywords() => Keywords?.Replace(" ","").Split(',');

	public static string GetCategoryString(SearchCategory category)
	{
		return category switch
		{
			SearchCategory.None => Lang.Resources.SearchCategory_NoCategory,
			SearchCategory.General => Lang.Resources.SearchCategory_General,
			SearchCategory.Wallet => Lang.Resources.SearchCategory_Wallet,
			SearchCategory.HelpAndSupport => Lang.Resources.SearchCategory_HelpAndSupport,
			SearchCategory.Open => Lang.Resources.SearchCategory_Open,
			SearchCategory.Settings => Lang.Resources.SearchCategory_Settings,
			SearchCategory.Transactions => Lang.Resources.SearchCategory_Transactions,
			_ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
		};
	}

	public string GetCategoryString() => GetCategoryString(Category);
};
