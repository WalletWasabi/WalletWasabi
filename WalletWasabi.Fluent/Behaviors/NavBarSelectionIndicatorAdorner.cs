using System;
using System.Reactive.Disposables;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectionIndicatorAdorner : Canvas, IDisposable
{
	private CompositeDisposable disposables = new();
	private readonly Canvas _layer;
	private readonly Control _target;
	private readonly NavBarSelectedIndicatorState _sharedState;
	private bool _isDispose;
	private Rectangle newRect = new Rectangle();

	public NavBarSelectionIndicatorAdorner(Control element, NavBarSelectedIndicatorState sharedState)
	{
		_target = element;
		_sharedState = sharedState;
		_layer = AdornerLayer.GetAdornerLayer(_target);

		IsHitTestVisible = false;
		ClipToBounds = true;

		if (_layer is null)
		{
			return;
		}

		Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Loaded);

		_sharedState.AdornerControl = this;

		_layer.Children.Add(this);
		Children.Add(newRect);

		newRect.VerticalAlignment = VerticalAlignment.Top;
		newRect.HorizontalAlignment = HorizontalAlignment.Left;
		VerticalAlignment = VerticalAlignment.Top;
		HorizontalAlignment = HorizontalAlignment.Left;

		_target.WhenAnyValue(x => x.Bounds)
			.Subscribe(x =>
			{
				var prevVector = _target.TranslatePoint(new Point(), VisualRoot) ?? new Point();
				Width = x.Width;
				Height = x.Height;
				Margin = new Thickness(prevVector.X, prevVector.Y, 0, 0);
			})
			.DisposeWith(disposables);

		newRect.Transitions = new()
		{
			new DoubleTransition
			{
				Property = Canvas.LeftProperty,
				Duration = TimeSpan.FromSeconds(1),
				Easing = new SplineEasing(0.1, 0.9, 0.2, 1)
			},
			new DoubleTransition
			{
				Property = Canvas.TopProperty,
				Duration = TimeSpan.FromSeconds(1),
				Easing = new SplineEasing(0.1, 0.9, 0.2, 1)
			}
		};
	}

	private Rect bounds;

	public void Dispose()
	{
		_isDispose = true;
		_layer?.Children.Remove(this);
	}

	public void AnimateIndicators(Rectangle previousIndicator, Rectangle nextIndicator, CancellationToken token,
		Orientation navItemsOrientation)
	{
		if (_isDispose)
		{
			return;
		}

		var nextVector = nextIndicator.TranslatePoint(new Point(), this) ?? new Point();
		newRect.Width = previousIndicator.Bounds.Width;
		newRect.Height = previousIndicator.Bounds.Height;
		newRect.Fill = previousIndicator.Fill;
		SetLeft(newRect, nextVector.X);
		SetTop(newRect, nextVector.Y);
	}

	public void InitialFix(Rectangle initial)
	{
		var prevVector = initial.TranslatePoint(new Point(), this) ?? new Point();
		newRect.Width = initial.Bounds.Width;
		newRect.Height = initial.Bounds.Height;
		newRect.Fill = initial.Fill;
		SetLeft(newRect, prevVector.X);
		SetTop(newRect, prevVector.Y);
	}
}