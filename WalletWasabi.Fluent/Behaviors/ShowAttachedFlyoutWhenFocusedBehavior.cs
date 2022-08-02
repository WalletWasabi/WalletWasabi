using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowAttachedFlyoutWhenFocusedBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<bool> IsFlyoutOpenProperty =
		AvaloniaProperty.Register<ShowAttachedFlyoutWhenFocusedBehavior, bool>(nameof(IsFlyoutOpen));

	private FlyoutController? _flyoutController;

	public bool IsFlyoutOpen
	{
		get => GetValue(IsFlyoutOpenProperty);
		set => SetValue(IsFlyoutOpenProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var flyoutBase = FlyoutBase.GetAttachedFlyout(AssociatedObject);
		if (flyoutBase is null)
		{
			return;
		}

		if (AssociatedObject.GetVisualRoot() is not Control visualRoot)
		{
			return;
		}

		_flyoutController = new FlyoutController(flyoutBase, AssociatedObject)
			.DisposeWith(disposable);

		DescendantPressed(visualRoot)
			.Select(descendant => AssociatedObject.IsVisualAncestorOf(descendant))
			.Do(
				isAncestor =>
				{
					_flyoutController.SetIsOpen(isAncestor);
					IsFlyoutOpen = isAncestor;
				})
			.Subscribe()
			.DisposeWith(disposable);

		Observable.FromEventPattern(AssociatedObject, nameof(InputElement.GotFocus))
			.Do(
				_ =>
				{
					_flyoutController.SetIsOpen(true);
					IsFlyoutOpen = true;
				})
			.Subscribe()
			.DisposeWith(disposable);

		Observable.FromEventPattern(visualRoot, nameof(Window.Activated))
			.Do(
				_ =>
				{
					if (AssociatedObject.IsFocused)
					{
						_flyoutController.SetIsOpen(true);
					}
				})
			.Subscribe()
			.DisposeWith(disposable);

		Observable.FromEventPattern(AssociatedObject, nameof(InputElement.LostFocus))
			.Where(_ => !IsFocusInside(flyoutBase))
			.Do(
				_ =>
				{
					_flyoutController.SetIsOpen(false);
					IsFlyoutOpen = false;
				})
			.Subscribe()
			.DisposeWith(disposable);

		this.GetObservable(IsFlyoutOpenProperty)
			.Do(b => _flyoutController.SetIsOpen(b))
			.Subscribe()
			.DisposeWith(disposable);

		// This is a workaround for the case when the user switches theme. The same behavior is detached and re-attached on theme changes.
		// If you don't close it, the Flyout will show in an incorrect position. Maybe bug in Avalonia?
		if (IsFlyoutOpen)
		{
			_flyoutController.SetIsOpen(false);
		}
	}

	private static IObservable<Visual> DescendantPressed(IInteractive interactive)
	{
		return interactive
			.OnEvent(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel)
			.Select(x => x.EventArgs.Source)
			.WhereNotNull()
			.OfType<Visual>()
			.Where(x => x is not LightDismissOverlayLayer);
	}

	private static bool IsFocusInside(IPopupHostProvider popupHostProvider)
	{
		var focusManager = FocusManager.Instance;

		if (focusManager?.Current is null)
		{
			return false;
		}

		var popupPresenter = popupHostProvider.PopupHost?.Presenter;

		if (popupPresenter is null)
		{
			return false;
		}

		var currentlyFocused = focusManager.Current;
		return popupPresenter.IsVisualAncestorOf(currentlyFocused);
	}
}
