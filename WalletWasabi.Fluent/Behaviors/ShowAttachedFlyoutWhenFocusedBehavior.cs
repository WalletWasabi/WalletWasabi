using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowAttachedFlyoutWhenFocusedBehavior : Behavior<Control>
{
	public static readonly StyledProperty<bool> IsFlyoutOpenProperty =
		AvaloniaProperty.Register<ShowAttachedFlyoutWhenFocusedBehavior, bool>(
			nameof(IsFlyoutOpen));

	private readonly CompositeDisposable _disposables = new();

	private FlyoutController? _flyoutController;

	public bool IsFlyoutOpen
	{
		get => GetValue(IsFlyoutOpenProperty);
		set => SetValue(IsFlyoutOpenProperty, value);
	}

	protected override void OnAttachedToVisualTree()
	{
		base.OnAttachedToVisualTree();

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
			.DisposeWith(_disposables);

		DescendantPressed(visualRoot)
			.Select(descendant => AssociatedObject.IsVisualAncestorOf(descendant))
			.Do(isAncestor =>
			{
				_flyoutController.IsOpen = isAncestor;
				IsFlyoutOpen = isAncestor;
			})
			.Subscribe()
			.DisposeWith(_disposables);

		Observable.FromEventPattern(AssociatedObject, nameof(InputElement.GotFocus))
			.Do(_ =>
			{
				_flyoutController.IsOpen = true;
				IsFlyoutOpen = true;
			})
			.Subscribe()
			.DisposeWith(_disposables);

		Observable.FromEventPattern(AssociatedObject, nameof(InputElement.LostFocus))
			.Where(_ => !IsFocusInside(flyoutBase))
			.Do(_ =>
			{
				_flyoutController.IsOpen = false;
				IsFlyoutOpen = false;
			})
			.Subscribe()
			.DisposeWith(_disposables);

		this.GetObservable(IsFlyoutOpenProperty).Subscribe(b => _flyoutController.IsOpen = b);

		if (IsFlyoutOpen)
		{
			_flyoutController.IsOpen = false;
		}
	}

	protected override void OnDetachedFromVisualTree()
	{
		_disposables.Dispose();
	}

	private static IObservable<Visual> DescendantPressed(IInteractive interactive)
	{
		return
			from eventPattern in interactive.OnEvent(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel)
			let source = eventPattern.EventArgs.Source as Visual
			where source is not null
			where source is not LightDismissOverlayLayer
			select source;
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