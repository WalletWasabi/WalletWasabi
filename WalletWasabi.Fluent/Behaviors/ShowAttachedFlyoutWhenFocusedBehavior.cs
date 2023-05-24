using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowAttachedFlyoutWhenFocusedBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly DirectProperty<ShowAttachedFlyoutWhenFocusedBehavior, bool> IsFlyoutOpenProperty =
		AvaloniaProperty.RegisterDirect<ShowAttachedFlyoutWhenFocusedBehavior, bool>(
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
		if (AssociatedObject?.GetVisualRoot() is not Control visualRoot)
		{
			return;
		}

		var flyoutBase = FlyoutBase.GetAttachedFlyout(AssociatedObject);
		if (flyoutBase is null)
		{
			return;
		}

		_ = new PreventFlyoutFromClosing(flyoutBase)
			.DisposeWith(disposable);

		FlyoutHelpers.ShowFlyout(AssociatedObject, flyoutBase, this.GetObservable(IsFlyoutOpenProperty), disposable);
		FocusBasedFlyoutOpener(AssociatedObject, flyoutBase).DisposeWith(disposable);
		
		// This is a workaround for the case when the user switches theme. The same behavior is detached and re-attached on theme changes.
		// If you don't close it, the Flyout will show in an incorrect position. Maybe bug in Avalonia?
		if (IsFlyoutOpen)
		{
			IsFlyoutOpen = false;
		}
	}

	private static IObservable<bool> GetPopupIsFocused(FlyoutBase flyoutBase)
	{
		var currentPopupHost = Observable
			.FromEventPattern(flyoutBase, nameof(flyoutBase.Opened))
			.Select(_ => ((IPopupHostProvider)flyoutBase).PopupHost?.Presenter)
			.WhereNotNull();

		var popupGotFocus = currentPopupHost.Select(x => x.OnEvent(InputElement.GotFocusEvent)).Switch().ToSignal();
		var popupLostFocus = currentPopupHost.Select(x => x.OnEvent(InputElement.LostFocusEvent)).Switch().ToSignal();
		var flyoutGotFocus = popupGotFocus.Select(_ => true).Merge(popupLostFocus.Select(_ => false));
		return flyoutGotFocus;
	}

	private IDisposable FocusBasedFlyoutOpener(
		IAvaloniaObject associatedObject,
		FlyoutBase flyoutBase)
	{
		var isPopupFocused = GetPopupIsFocused(flyoutBase);
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

	private class PreventFlyoutFromClosing : IDisposable
	{
		public PreventFlyoutFromClosing(FlyoutBase flyout)
		{
			Flyout = flyout;
			Flyout.Closing += FlyoutClosing;
		}
		
		public FlyoutBase Flyout { get; }
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
