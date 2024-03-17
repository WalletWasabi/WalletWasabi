using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowFlyoutWhenFocusedBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly DirectProperty<ShowFlyoutWhenFocusedBehavior, bool> IsFlyoutOpenProperty =
		AvaloniaProperty.RegisterDirect<ShowFlyoutWhenFocusedBehavior, bool>(
			"IsFlyoutOpen",
			o => o.IsFlyoutOpen,
			(o, v) => o.IsFlyoutOpen = v);

	private bool _isFlyoutOpen;

	public bool IsFlyoutOpen
	{
		get => _isFlyoutOpen;
		set => SetAndRaise(IsFlyoutOpenProperty, ref _isFlyoutOpen, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject?.GetVisualRoot() is Control &&
			FlyoutBase.GetAttachedFlyout(AssociatedObject) is Flyout flyout)
		{
			var flyoutController = new FlyoutController(flyout)
				.DisposeWith(disposable);

			FlyoutHelpers.ShowFlyout(AssociatedObject, flyout, this.GetObservable(IsFlyoutOpenProperty), disposable);
			FocusBasedFlyoutOpener(AssociatedObject, flyoutController).DisposeWith(disposable);

			// This is a workaround for the case when the user switches theme. The same behavior is detached and re-attached on theme changes.
			// If you don't close it, the Flyout will show in an incorrect position. Maybe bug in Avalonia?
			if (IsFlyoutOpen)
			{
				IsFlyoutOpen = false;
			}
		}
	}

	private static IObservable<bool> GetPopupIsFocused(FlyoutBase flyout)
	{
		var currentPopupHost = Observable
			.FromEventPattern(flyout, nameof(flyout.Opened))
			.Select(_ => ((IPopupHostProvider)flyout).PopupHost?.Presenter)
			.WhereNotNull();

		var popupGotFocus = currentPopupHost.Select(x => x.OnEvent(InputElement.GotFocusEvent)).Switch();
		var popupLostFocus = currentPopupHost.Select(x => x.OnEvent(InputElement.LostFocusEvent)).Switch();
		var flyoutGotFocus = popupGotFocus.Select(_ => true).Merge(popupLostFocus.Select(_ => false));
		return flyoutGotFocus;
	}

	private IDisposable FocusBasedFlyoutOpener(Control associatedObject, FlyoutController flyoutController)
	{
		var isPopupFocused = GetPopupIsFocused(flyoutController.Flyout);
		var isAssociatedObjectFocused = associatedObject.GetObservable(InputElement.IsFocusedProperty);

		var mergedFocused = isAssociatedObjectFocused.Merge(isPopupFocused);

		var weAreFocused = mergedFocused
			.Throttle(TimeSpan.FromSeconds(0.1))
			.DistinctUntilChanged();

		return weAreFocused
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(isOpen => IsFlyoutOpen = isOpen)
			.Subscribe();
	}

	private class FlyoutController : IDisposable
	{
		public FlyoutController(PopupFlyoutBase flyout)
		{
			Flyout = flyout;
			Flyout.Closing += FlyoutClosing;
		}

		public PopupFlyoutBase Flyout { get; }
		public bool PreventClose { get; set; }

		public void Dispose()
		{
			Flyout.Closing -= FlyoutClosing;
		}

		private void FlyoutClosing(object? sender, CancelEventArgs e)
		{
			e.Cancel = PreventClose;
		}
	}
}
