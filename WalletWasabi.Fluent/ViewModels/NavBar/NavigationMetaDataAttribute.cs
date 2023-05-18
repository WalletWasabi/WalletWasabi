namespace WalletWasabi.Fluent;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NavigationMetaDataAttribute : Attribute
{
	public bool Searchable { get; set; }

	public string? Title { get; set; }

	public string? Caption { get; set; }

	public string? IconName { get; set; }

	public string? IconNameFocused { get; set; }

	public int Order { get; set; }

	public string? Category { get; set; }

	public string[]? Keywords { get; set; }

	public NavBarPosition NavBarPosition { get; set; }

	public NavBarSelectionMode NavBarSelectionMode { get; set; }

	public NavigationTarget NavigationTarget { get; set; }
}
