using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowSliderThumbToolTipBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<Slider?> SliderProperty =
		AvaloniaProperty.Register<ShowSliderThumbToolTipBehavior, Slider?>(nameof(Slider));

	public Slider? Slider
	{
		get => GetValue(SliderProperty);
		set => SetValue(SliderProperty, value);
	}

	protected override IDisposable OnAttachedOverride()
	{
		if (AssociatedObject is null || Slider is null)
		{
			return Disposable.Empty;
		}

		var disposables = new CompositeDisposable();

		Observable
			.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerPressed))
			.Subscribe(_ => SetSliderTooTipIsOpen(true))
			.DisposeWith(disposables);

		Observable
			.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerReleased))
			.Subscribe(_ => SetSliderTooTipIsOpen(false))
			.DisposeWith(disposables);

		Observable
			.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerCaptureLost))
			.Subscribe(_ => SetSliderTooTipIsOpen(false))
			.DisposeWith(disposables);

		Slider.AddHandler(InputElement.PointerPressedEvent, PointerPressed, RoutingStrategies.Tunnel);
		Slider.AddHandler(InputElement.PointerReleasedEvent, PointerReleased, RoutingStrategies.Tunnel);
		Slider.AddHandler(InputElement.PointerCaptureLostEvent, PointerCaptureLost, RoutingStrategies.Tunnel);

		return disposables;
	}

	protected override void OnDetaching()
	{
		SetSliderTooTipIsOpen(false);

		Slider?.RemoveHandler(InputElement.PointerPressedEvent, PointerPressed);
		Slider?.RemoveHandler(InputElement.PointerReleasedEvent, PointerReleased);
		Slider?.RemoveHandler(InputElement.PointerCaptureLostEvent, PointerCaptureLost);

		base.OnDetaching();
	}

	private void PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is not Thumb)
		{
			SetSliderTooTipIsOpen(true);
		}
	}

	private void PointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (sender is not Thumb)
		{
			SetSliderTooTipIsOpen(false);
		}
	}

	private void PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		SetSliderTooTipIsOpen(false);
	}

	private void SetSliderTooTipIsOpen(bool isOpen)
	{
		var thumb = Slider?.GetVisualDescendants()?.OfType<Thumb>().FirstOrDefault();
		if (thumb is { })
		{
			var toolTip = ToolTip.GetTip(thumb);
			if (toolTip is ToolTip)
			{
				thumb.SetValue(ToolTip.IsOpenProperty, isOpen);
			}
		}
	}
}
