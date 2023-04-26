using System.ComponentModel;

namespace WalletWasabi.Fluent;

public enum NavBarPosition
{
	None,
	Top,
	Bottom
}

public enum NavigationTarget
{
	Default = 0,
	HomeScreen = 1,
	DialogScreen = 2,
	FullScreen = 3,
	CompactDialogScreen = 4,
}

public enum NavBarSelectionMode
{
	Selected = 0,
	Button = 1,
	Toggle = 2
}

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

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NavigationMetaDataAttribute : Attribute
{
	public NavigationMetaDataAttribute()
	{
	}

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

public interface INavBarItem : INotifyPropertyChanged
{
	string Title { get; }
	string IconName { get; }
	string IconNameFocused { get; }
}

public interface INavBarToggle : INavBarItem
{
	void Toggle();
}

public interface INavBarButton : INavBarItem
{
	void Activate();
}
