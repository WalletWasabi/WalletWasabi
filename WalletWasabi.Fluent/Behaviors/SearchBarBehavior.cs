using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class SearchBarBehavior : AttachedToVisualTreeBehavior<UserControl>
{
	private TopLevel? _topLevel;

	public static readonly StyledProperty<TextBox?> SearchBoxProperty =
		AvaloniaProperty.Register<SearchBarBehavior, TextBox?>(nameof(SearchBox));

	[ResolveByName]
	public TextBox? SearchBox
	{
		get => GetValue(SearchBoxProperty);
		set => SetValue(SearchBoxProperty, value);
	}

	public static readonly StyledProperty<UserControl?> SearchPanelProperty =
		AvaloniaProperty.Register<SearchBarBehavior, UserControl?>(nameof(SearchPanel));

	[ResolveByName]
	public UserControl? SearchPanel
	{
		get => GetValue(SearchPanelProperty);
		set => SetValue(SearchPanelProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
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

	private void SearchBoxOnGotFocus(object? sender, GotFocusEventArgs e)
	{
		if (SearchPanel is { })
		{
			SearchPanel.IsVisible = true;
		}
	}

	private void AssociatedObjectOnLostFocus(object? sender, RoutedEventArgs e)
	{
		if (AssociatedObject is { } && SearchPanel is { })
		{
			if (!AssociatedObject.IsKeyboardFocusWithin)
			{
				SearchPanel.IsVisible = false;
			}
		}
	}

	private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if(AssociatedObject is { } && !AssociatedObject.IsPointerOver)
		{
			if (ReferenceEquals(FocusManager.Instance?.Current, SearchBox))
			{
				FocusManager.Instance?.Focus(null);
			}
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
}