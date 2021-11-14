using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorState : IDisposable
{
	public readonly ConcurrentDictionary<int, Control> ScopeChildren = new();
	public Rectangle? PreviousIndicator { get; set; }
	public NavBarSelectionIndicatorAdorner AdornerControl { get; set; }

	public Orientation NavItemsOrientation { get; set; } = Orientation.Vertical;

	public void Dispose()
	{
		ScopeChildren?.Clear();
	}

	public void AddChild(Control associatedObject)
	{
		if (ScopeChildren.ContainsKey(associatedObject.GetHashCode()))
		{
			return;
		}

		ScopeChildren.TryAdd(associatedObject.GetHashCode(),
			associatedObject);
	}

	public void Animate(Rectangle NextIndicator)
	{
		// For Debouncing.
		if (PreviousIndicator == NextIndicator)
		{
			return;
		}

		var root = PreviousIndicator.GetVisualAncestors().OfType<VisualLayerManager>().FirstOrDefault();

		var prevVector = PreviousIndicator.TranslatePoint(new Point(), root) ?? new Point();
		var nextVector = NextIndicator.TranslatePoint(new Point(), root) ?? new Point();

		_currentAnimationCts?.Cancel();
		AdornerControl.AnimateIndicators(PreviousIndicator, prevVector, NextIndicator, nextVector,
			_currentAnimationCts.Token, NavItemsOrientation);
		_currentAnimationCts = new();

		PreviousIndicator = NextIndicator;
	}

	private CancellationTokenSource _currentAnimationCts = new();
}