using System;
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

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectionIndicatorAdorner : Control, IDisposable
{
	private readonly AdornerLayer _layer;
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
		IsVisible = false;

		if (_layer is null)
		{
			return;
		}

		Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Loaded);

		_sharedState.AdornerControl = this;
		_layer.Children.Add(this);
		_layer.Children.Add(newRect);

		newRect.VerticalAlignment = VerticalAlignment.Top;
		newRect.HorizontalAlignment = HorizontalAlignment.Left;

		if (Application.Current.Styles.TryGetResource("FluentEasing", out object? resource) &&
		    resource is Easing outEasing)
		{
			newRect.Transitions = new Transitions()
			{
				new ThicknessTransition()
				{
					Property = Rectangle.MarginProperty,
					Duration = TimeSpan.FromSeconds(0.30),
					Easing = outEasing
				}
			};
		}
	}

#if DEBUG

	public override void Render(DrawingContext context)
	{
		Rect adornedElementRect = _target.Bounds;

		var renderBrush = new SolidColorBrush(Colors.Green)
		{
			Opacity = 0.2
		};
		var renderPen = new Pen(new SolidColorBrush(Colors.Navy), 1.5);
		var renderRadius = 5.0;

		context.DrawRectangle(renderBrush, renderPen, adornedElementRect);
	}

#endif

	public void Dispose()
	{
		_isDispose = true;
		_layer?.Children.Remove(this);
		_layer?.Children.Remove(newRect);
	}

	public void AnimateIndicators(Rectangle previousIndicator, Point prevVector, Rectangle nextIndicator,
		Point nextVector, CancellationToken token, Orientation navItemsOrientation)
	{
		if (_isDispose)
		{
			return;
		}

		newRect.Width = previousIndicator.Bounds.Width;
		newRect.Height = previousIndicator.Bounds.Height;
		newRect.Fill = previousIndicator.Fill;
		newRect.Margin = new Thickness(nextVector.X, nextVector.Y, 0, 0);
	}
}