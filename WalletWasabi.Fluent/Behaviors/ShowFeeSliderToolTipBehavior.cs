using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors
{
	public class ShowFeeSliderToolTipBehavior : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<Slider?> SliderProperty =
			AvaloniaProperty.Register<ShowFeeSliderToolTipBehavior, Slider?>(nameof(Slider));

		public Slider? Slider
		{
			get => GetValue(SliderProperty);
			set => SetValue(SliderProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

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
		}

		protected override void OnDetaching()
		{
			SetSliderTooTipIsOpen(false);

			base.OnDetaching();
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
}