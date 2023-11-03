using Avalonia;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Helpers;

public enum Theme
{
	Dark,
	Light
}

public static class ThemeHelper
{
	public static Theme CurrentTheme { get; private set; }

	public static void ApplyTheme(Theme theme)
	{
		if (Application.Current is { } current)
		{
			CurrentTheme = theme;
			current.RequestedThemeVariant = theme == Theme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
		}
	}
}
