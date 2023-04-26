namespace WalletWasabi.Fluent;

public sealed class NavigationMetaData
{
	public bool Searchable { get; init; } = true;

	public string? Title { get; init; }

	public string? Caption { get; init; }

	public string? IconName { get; init; }

	public string? IconNameFocused { get; init; }

	public int Order { get; init; }

	public string? Category { get; init; }

	public string[]? Keywords { get; init; }

	public NavBarPosition NavBarPosition { get; init; }

	public NavBarSelectionMode NavBarSelectionMode { get; init; }

	public NavigationTarget NavigationTarget { get; init; }
}
