using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Controls;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>Geometric design contracts, independent of raster enrollment.</summary>
public sealed class MobileReferenceLayoutTests
{
	[AvaloniaTheory]
	[InlineData(320, false)]
	[InlineData(320, true)]
	[InlineData(390, false)]
	[InlineData(390, true)]
	public void PageTitleRemainsCenteredWithAsymmetricActions(int width, bool back)
	{
		var page = new MobilePage { Header = "Receive Bitcoin", ShowBack = back };
		var window = new Window { Width = width, Height = 844, Content = page, SystemDecorations = SystemDecorations.None };
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			foreach (var actions in new Control?[] { null, new Button { Content = "…", Width = 48 }, null })
			{
				page.HeaderActions = actions;
				Dispatcher.UIThread.RunJobs();
				var presenter = page.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "PART_HeaderPresenter");
				var center = presenter.TranslatePoint(new Point(presenter.Bounds.Width / 2, 0), window);
				Assert.NotNull(center);
				Assert.InRange(center.Value.X, width / 2d - 0.5, width / 2d + 0.5);
			}
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void QuickActionsUseThemeSpecificTilesButKeepTheSameTouchGeometry()
	{
		var button = new MobileActionButton { Label = "Receive", Icon = "receive", Classes = { "quick" }, Width = 76 };
		var window = new Window { Width = 390, Height = 844, Content = button, RequestedThemeVariant = ThemeVariant.Light };
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			var icon = button.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_IconCircle");
			Assert.Equal(new Size(44, 44), icon.Bounds.Size);
			Assert.True(button.MinHeight >= 48);
			Assert.Equal((byte)0, Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color.A);
			window.RequestedThemeVariant = ThemeVariant.Dark;
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(new Size(44, 44), icon.Bounds.Size);
			Assert.Equal((byte)255, Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color.A);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void LongPageTitleWrapsWithinItsReservedHeaderColumn()
	{
		var page = new MobilePage { Header = "Addresses awaiting payment", ShowBack = true };
		var window = new Window { Width = 320, Height = 568, Content = page, SystemDecorations = SystemDecorations.None };
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			var presenter = page.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "PART_HeaderPresenter");
			var text = Assert.Single(presenter.GetVisualDescendants().OfType<TextBlock>());
			Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
			Assert.True(text.Bounds.Width <= presenter.Bounds.Width);
			Assert.True(text.Bounds.Height <= page.Bounds.Height);
		}
		finally { window.Close(); }
	}
}
