using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class SearchBarBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<Control?> SearchBoxProperty =
		AvaloniaProperty.Register<SearchBarBehavior, Control?>(nameof(SearchBox));

	public static readonly StyledProperty<Control?> SearchPanelProperty =
		AvaloniaProperty.Register<SearchBarBehavior, Control?>(nameof(SearchPanel));

	public static readonly StyledProperty<bool> IsSearchPanelOpenProperty =
		AvaloniaProperty.Register<SearchBarBehavior, bool>(nameof(SearchBox));

	[ResolveByName]
	public Control? SearchBox
	{
		get => GetValue(SearchBoxProperty);
		set => SetValue(SearchBoxProperty, value);
	}

	[ResolveByName]
	public Control? SearchPanel
	{
		get => GetValue(SearchPanelProperty);
		set => SetValue(SearchPanelProperty, value);
	}

	public bool IsSearchPanelOpen
	{
		get => GetValue(IsSearchPanelOpenProperty);
		set => SetValue(IsSearchPanelOpenProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
		this.GetObservable(IsSearchPanelOpenProperty)
			.Subscribe(ToggleFlyoutOpen);

		if (AssociatedObject is null)
		{
			return;
		}

		var visualRoot = AssociatedObject.GetVisualRoot();

		if (visualRoot is TopLevel topLevel)
		{
			topLevel
				.AddDisposableHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel)
				.DisposeWith(disposables);
		}

		if (visualRoot is WindowBase window)
		{
			Observable
				.FromEventPattern(window, nameof(WindowBase.Deactivated))
				.Subscribe(_ =>
				{
					FocusManager.Instance?.Focus(null);
					HideFlyout();
				})
				.DisposeWith(disposables);
		}

		if (SearchBox is { } && SearchPanel is { })
		{
			Observable
				.FromEventPattern(SearchBox, nameof(SearchBox.GotFocus))
				.Subscribe(_ => SearchBoxOnGotFocus())
				.DisposeWith(disposables);

			Observable
				.FromEventPattern(SearchBox, nameof(SearchBox.LostFocus))
				.Subscribe(_ => AssociatedObjectOnLostFocus())
				.DisposeWith(disposables);
		}
	}

	private void ToggleFlyoutOpen(bool isOpen)
	{
		if (isOpen)
		{
			ShowFlyout();
		}
		else
		{
			HideFlyout();
		}
	}

	private void SearchBoxOnGotFocus()
	{
		ShowFlyout();
	}

	private void ShowFlyout()
	{
		if (AssociatedObject != null)
		{
			FlyoutBase.ShowAttachedFlyout(AssociatedObject);
		}
	}

	private void AssociatedObjectOnLostFocus()
	{
		if (AssociatedObject is { } && SearchPanel is { } &&
			!AssociatedObject.IsKeyboardFocusWithin && !SearchPanel.IsKeyboardFocusWithin)
		{
			HideFlyout();
		}
	}

	private void HideFlyout()
	{
		if (AssociatedObject is null)
		{
			return;
		}

		FlyoutBase.GetAttachedFlyout(AssociatedObject)?.Hide();
	}

	private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (AssociatedObject is { IsPointerOver: false } && ReferenceEquals(FocusManager.Instance?.Current, SearchBox))
		{
			FocusManager.Instance?.Focus(null);
		}
	}
}
