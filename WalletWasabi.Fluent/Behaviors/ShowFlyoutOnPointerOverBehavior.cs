using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class BindableFlyoutOpenBehavior : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<bool> IsOpenProperty =
			AvaloniaProperty.Register<BindableFlyoutOpenBehavior, bool>( nameof(IsOpen));

		public bool IsOpen
		{
			get => GetValue(IsOpenProperty);
			set => SetValue(IsOpenProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposable)
		{
			Observable.FromEventPattern(AssociatedObject!, nameof(AssociatedObject.PointerPressed))
				.Subscribe(x =>
				{
					FlyoutBase.ShowAttachedFlyout(AssociatedObject!);
				}).DisposeWith(disposable);

			this.WhenAnyValue(x => x.IsOpen)
				.Subscribe(x =>
				{
					if(x)
					{
						FlyoutBase.ShowAttachedFlyout(AssociatedObject!);
					}
					else if(Flyout.GetAttachedFlyout(AssociatedObject) is FlyoutBase flyout)
					{
						Flyout.GetAttachedFlyout(AssociatedObject!)?.Hide();
					}
				});
		}


	}
	public class ShowFlyoutOnPointerOverBehavior : DisposingBehavior<Control>
	{
		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			Observable
				.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerEnter))
				.Subscribe(_ => FlyoutBase.ShowAttachedFlyout(AssociatedObject))
				.DisposeWith(disposables);
		}
	}
}
