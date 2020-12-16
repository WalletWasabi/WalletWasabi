using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace WalletWasabi.Fluent.Helpers
{
	public static class ThemeHelper
	{
		public static void ApplyTheme(bool darkMode)
		{
			var currentTheme = Application.Current.Styles.Select(x => (StyleInclude)x).FirstOrDefault(x => x.Source is { } && x.Source.AbsolutePath.Contains("Themes"));

			if (currentTheme is { })
			{
				var themeIndex = Application.Current.Styles.IndexOf(currentTheme);

				var newTheme = new StyleInclude(new Uri("avares://WalletWasabi.Fluent/App.axaml"))
				{
					Source = new Uri($"avares://WalletWasabi.Fluent/Styles/Themes/{(darkMode ? "BaseDark" : "BaseLight")}.axaml")
				};

				Application.Current.Styles[themeIndex] = newTheme;
			}
		}
	}
}