using System;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Draggable;
using Microsoft.AspNetCore.Components.Forms;
using NBitcoin.OpenAsset;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorParentBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly AttachedProperty<NavBarSelectedIndicatorState>
		ParentStateProperty =
			AvaloniaProperty.RegisterAttached<Control, Control, NavBarSelectedIndicatorState>("ParentState",
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
		var z = new TestAdorner(AssociatedObject);
		Dispatcher.UIThread.Post(z.InvalidateVisual, DispatcherPriority.Loaded);
	}
}

public class TestAdorner : Control, IDisposable
{
	private readonly AdornerLayer _layer;
	private readonly Control _target;

	public TestAdorner(Control element)
	{
		_target = element;
		_layer = AdornerLayer.GetAdornerLayer(_target);

		IsHitTestVisible = false;

		if (_layer is null)
		{
			return;
		}

		_layer.Children.Add(this);
	}


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

	public void Dispose()
	{
		if (_layer is not null)
		{
			_layer.Children.Remove(this);
		}
	}
}