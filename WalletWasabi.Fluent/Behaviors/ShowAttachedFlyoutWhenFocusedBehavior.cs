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
					_flyoutController.IsOpen = isAncestor;
					IsFlyoutOpen = isAncestor;
				})
			.Subscribe()
			.DisposeWith(disposable);

		Observable.FromEventPattern(AssociatedObject, nameof(InputElement.GotFocus))
			.Do(
				_ =>
				{
					_flyoutController.IsOpen = true;
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
						_flyoutController.IsOpen = true;
					}
				})
			.Subscribe()
			.DisposeWith(disposable);

		Observable.FromEventPattern(AssociatedObject, nameof(InputElement.LostFocus))
			.Where(_ => !IsFocusInside(flyoutBase))
			.Do(
				_ =>
				{
					_flyoutController.IsOpen = false;
					IsFlyoutOpen = false;
				})
			.Subscribe()
			.DisposeWith(disposable);

		this.GetObservable(IsFlyoutOpenProperty).Subscribe(b => _flyoutController.IsOpen = b);

		if (IsFlyoutOpen)
		{
			_flyoutController.IsOpen = false;
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
