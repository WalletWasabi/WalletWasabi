using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

/// <summary>
/// Works around an Avalonia.Controls.TreeDataGrid layout bug (fixed upstream only in the commercially-licensed
/// 11.3.1 release, so the fix is unavailable to the pinned 11.1.1 package): when a star-sized column gets its
/// width committed before the trailing Auto columns have ever been measured, the committed column widths fill
/// the viewport exactly and the trailing cells begin precisely at the viewport's right edge. The horizontal
/// virtualization loop only realizes cells that start strictly inside the viewport, so those cells are never
/// created or measured - the column keeps an estimated width, the grid reports phantom horizontal overflow
/// and the right-side buttons stay invisible until the user drags the horizontal scrollbar.
/// See https://github.com/WalletWasabi/WalletWasabi/issues/14632.
/// When this behavior detects a never-measured column together with horizontal overflow, it briefly reports
/// a slightly narrower viewport to the columns: the star column shrinks, the missing cells start inside the
/// real viewport and finally get realized and measured. Once the layout settles the real viewport width is
/// restored, which lets the columns settle to their correct sizes and the phantom overflow disappear. If a
/// nudge brings no progress the content genuinely overflows, so the behavior stops interfering.
/// </summary>
public class TreeDataGridMissingCellsWorkaroundBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
	private const int MaxAttempts = 3;

	private int _attempts;
	private bool _nudgePending;
	private int _unmeasuredCountAtNudge;

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is not { } grid)
		{
			return Disposable.Empty;
		}

		grid.LayoutUpdated += OnLayoutUpdated;

		var sourceChanged = grid
			.GetObservable(Avalonia.Controls.TreeDataGrid.SourceProperty)
			.Subscribe(_ =>
			{
				_attempts = 0;
				_nudgePending = false;
			});

		return Disposable.Create(() =>
		{
			grid.LayoutUpdated -= OnLayoutUpdated;
			sourceChanged.Dispose();
		});
	}

	private void OnLayoutUpdated(object? sender, EventArgs e)
	{
		if (_nudgePending || _attempts >= MaxAttempts)
		{
			return;
		}

		var unmeasuredCount = CountUnmeasuredColumns();

		if (unmeasuredCount == 0 || AssociatedObject is not { Scroll: { } scroll, Source.Columns: { } columns })
		{
			return;
		}

		if (scroll.Viewport.Width <= 0 || scroll.Extent.Width <= scroll.Viewport.Width)
		{
			// Empty grid or everything fits: the unmeasured columns are not caused by the bug.
			return;
		}

		_attempts++;
		_nudgePending = true;
		_unmeasuredCountAtNudge = unmeasuredCount;

		// Pretend the viewport is slightly narrower: the star column shrinks, which makes the trailing
		// cells start inside the real viewport, so the virtualization finally realizes and measures them.
		columns.ViewportChanged(new Rect(0, 0, scroll.Viewport.Width - 2, scroll.Viewport.Height));

		// Restore the real width only after the layout triggered by the shrink has fully settled.
		Dispatcher.UIThread.Post(OnAfterNudge, DispatcherPriority.Background);
	}

	private void OnAfterNudge()
	{
		_nudgePending = false;

		if (AssociatedObject is not { Scroll: { } scroll, Source.Columns: { } columns })
		{
			return;
		}

		// Restore the real viewport width; with the previously missing cells now measured the columns
		// settle to their correct sizes. If nothing was gained the content genuinely overflows, so stop.
		columns.ViewportChanged(new Rect(0, 0, scroll.Viewport.Width, scroll.Viewport.Height));

		var unmeasuredCount = CountUnmeasuredColumns();
		if (unmeasuredCount >= _unmeasuredCountAtNudge)
		{
			_attempts = MaxAttempts;
		}
	}

	private int CountUnmeasuredColumns()
	{
		if (AssociatedObject is not { Source.Columns: { Count: > 0 } columns })
		{
			return 0;
		}

		var unmeasuredCount = 0;
		for (var i = 0; i < columns.Count; i++)
		{
			if (double.IsNaN(columns[i].ActualWidth))
			{
				unmeasuredCount++;
			}
		}

		return unmeasuredCount;
	}
}
