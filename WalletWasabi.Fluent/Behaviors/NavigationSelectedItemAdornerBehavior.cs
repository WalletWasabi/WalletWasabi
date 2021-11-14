using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using ReactiveUI;
using WalletWasabi.Logging;
using Pen = Avalonia.Media.Pen;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorParentBehavior : AttachedToVisualTreeBehavior<Control>
{
	public readonly CompositeDisposable disposables = new();

	public static readonly AttachedProperty<NavBarSelectedIndicatorState>
		ParentStateProperty =
			AvaloniaProperty
				.RegisterAttached<NavBarSelectedIndicatorParentBehavior, Control, NavBarSelectedIndicatorState>(
					"ParentState",
					inherits: true);

	public static NavBarSelectedIndicatorState GetParentState(Control element)
	{
		return element.GetValue(ParentStateProperty);
	}

	public static void SetParentState(Control element, NavBarSelectedIndicatorState value)
	{
		element.SetValue(ParentStateProperty, value);
	}

	protected override void OnAttachedToVisualTree()
	{
		var k = new NavBarSelectedIndicatorState();

		SetParentState(AssociatedObject, k);
		var z = new NavBarSelectionIndicatorAdorner(AssociatedObject, k);

		disposables.Add(k);
		disposables.Add(z);

		AssociatedObject.DetachedFromVisualTree += delegate { disposables.Dispose(); };
	}
}

public class NavBarSelectedIndicatorChildBehavior : AttachedToVisualTreeBehavior<Rectangle>
{
	public static readonly AttachedProperty<bool>
		IsSelectedProperty =
			AvaloniaProperty.RegisterAttached<NavBarSelectedIndicatorChildBehavior, Rectangle, bool>("IsSelected",
				inherits: true);

	public static bool GetIsSelected(Control element)
	{
		return element.GetValue(IsSelectedProperty);
	}

	public static void SetIsSelected(Control element, bool value)
	{
		element.SetValue(IsSelectedProperty, value);
	}


	public static readonly AttachedProperty<Control>
		NavBarItemParentProperty =
			AvaloniaProperty.RegisterAttached<NavBarSelectedIndicatorChildBehavior, Control, Control>(
				"NavBarItemParent");

	public static Control GetNavBarItemParent(Control element)
	{
		return element.GetValue(NavBarItemParentProperty);
	}

	public static void SetNavBarItemParent(Control element, Control value)
	{
		element.SetValue(NavBarItemParentProperty, value);
	}


	private NavBarSelectedIndicatorState GetSharedState =>
		NavBarSelectedIndicatorParentBehavior.GetParentState(AssociatedObject);


	protected override void OnAttachedToVisualTree()
	{
		if (GetSharedState is null)
		{
			Detach();
			return;
		}

		GetSharedState.AddChild(AssociatedObject);

		AssociatedObject.DetachedFromVisualTree += delegate
		{
			GetSharedState.ScopeChildren.TryRemove(AssociatedObject.GetHashCode(), out _);
		};

		var parent = GetNavBarItemParent(AssociatedObject);

		if (parent is null)
		{
			Logger.LogError(
				$"NavBarItem Selection Indicator's parent is null, cannot continue with indicator animations.");
			return;
		}

		if (parent.Classes.Contains(":selected"))
		{
			GetSharedState.PreviousIndicator = AssociatedObject;
		}

		AssociatedObject.GetPropertyChangedObservable(IsSelectedProperty)
			.DistinctUntilChanged()
			.Subscribe(x =>
			{
				var parent = GetNavBarItemParent(AssociatedObject);

				if ((bool)x.NewValue && (GetNavBarItemParent(AssociatedObject)?.Classes.Contains(":selected") ?? false))
				{
					GetSharedState.Animate(AssociatedObject);
				}
			});

		AssociatedObject.Opacity = 0;
	}
}

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