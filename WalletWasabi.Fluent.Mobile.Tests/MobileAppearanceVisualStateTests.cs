using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileAppearanceVisualStateTests
{
	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void ChosenAppearanceHasTheSemanticAccentSurface(bool dark)
	{
		using var errors = new MobileScreenshot.BindingErrors();
		var panel = new MobilePreferencesPanel { IsDark = dark, IsReadOnly = false };
		var window = new Window { Width = 390, Height = 844, Content = panel,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			var control = panel.FindControl<MobileAppearanceSelector>("Appearance")!;
			var light = control.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Light");
			var night = control.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Dark");
			var accent = Color.Parse(dark ? "#1D3426" : "#E8F4EE");
			foreach (var selection in new[] { false, true, false })
			{
				panel.IsDark = selection;
				Dispatcher.UIThread.RunJobs(); MobileSnapshotState.Prepare(window);
				Assert.Equal(!selection, light.Classes.Contains("selected"));
				Assert.Equal(selection, night.Classes.Contains("selected"));
				Assert.Equal(accent, Assert.IsAssignableFrom<ISolidColorBrush>((selection ? night : light).Background).Color);
				Assert.NotEqual(accent, Assert.IsAssignableFrom<ISolidColorBrush>((selection ? light : night).Background).Color);
			}
			Assert.Empty(errors.Errors);
		}
		finally { window.Close(); }
	}
}
