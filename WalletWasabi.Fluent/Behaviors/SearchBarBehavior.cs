using System.Reactive.Disposables;
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

	private TopLevel? _topLevel;

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

		if (AssociatedObject.GetVisualRoot() is TopLevel topLevel)
		{
			_topLevel = topLevel;

			topLevel.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel);
		}

		if (SearchBox is { } && SearchPanel is { })
		{
			SearchBox.GotFocus += SearchBoxOnGotFocus;
			AssociatedObject.LostFocus += AssociatedObjectOnLostFocus;
		}
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		if (_topLevel is { })
		{
			_topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed);

			_topLevel = null;
		}

		if (SearchBox is { })
		{
			SearchBox.GotFocus -= SearchBoxOnGotFocus;
		}

		if (AssociatedObject is { })
		{
			AssociatedObject.LostFocus -= AssociatedObjectOnLostFocus;
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

	private void SearchBoxOnGotFocus(object? sender, GotFocusEventArgs e)
	{
		ShowFlyout();
	}

	private void ShowFlyout()
	{
		FlyoutBase.ShowAttachedFlyout(AssociatedObject);
		IsSearchPanelOpen = true;
	}

	private void AssociatedObjectOnLostFocus(object? sender, RoutedEventArgs e)
	{
		if (AssociatedObject is { } && SearchPanel is { })
		{
			if (!AssociatedObject.IsKeyboardFocusWithin && !SearchPanel.IsKeyboardFocusWithin)
			{
				HideFlyout();
			}
		}
	}

	private void HideFlyout()
	{
		FlyoutBase.GetAttachedFlyout(AssociatedObject).Hide();
		IsSearchPanelOpen = false;
	}

	private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (AssociatedObject is { } && !AssociatedObject.IsPointerOver)
		{
			if (ReferenceEquals(FocusManager.Instance?.Current, SearchBox))
			{
				FocusManager.Instance?.Focus(null);
			}
		}
	}
}