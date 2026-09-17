using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileSnapshotStateTests
{
	[AvaloniaFact]
	public void ExpanderSnapshotsUseTheFinalAngleOfEachActualState()
	{
		var expander = new Expander { Header = "Snapshot state", Content = new TextBlock { Text = "Expanded content" } };
		var window = new Window { Width = 390, Height = 844, Content = expander };
		try
		{
			window.Show();
			foreach (var expanded in new[] { false, true, false })
			{
				expander.IsExpanded = expanded;
				for (var pass = 0; pass < 4; pass++)
				{
					Dispatcher.UIThread.RunJobs();
					MobileSnapshotState.Prepare(window);
					AvaloniaHeadlessPlatform.ForceRenderTimerTick();
					var chevron = Assert.Single(expander.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>(),
						path => path.Name == "ExpandCollapseChevron");
					Assert.Equal(expanded ? 180d : 0d, Assert.IsType<RotateTransform>(chevron.RenderTransform).Angle);
					Assert.Equal(expanded, expander.IsExpanded);
				}
			}
		}
		finally { window.Close(); }
	}
}
