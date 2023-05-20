#pragma warning disable IDE0130 // Namespace does not match folder structure (see https://github.com/zkSNACKs/WalletWasabi/pull/10576#issuecomment-1552750543)

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
