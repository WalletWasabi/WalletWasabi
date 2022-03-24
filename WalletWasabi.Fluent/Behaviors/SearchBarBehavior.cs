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
		if (AssociatedObject.GetVisualRoot() is TopLevel topLevel)
		{
			topLevel.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel);

			disposables.Add(Disposable.Create(()=>topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPointerPressed)));
		}

		if (SearchBox is { } && SearchPanel is { })
		{
			disposables.Add(Observable.FromEventPattern(SearchBox, nameof(SearchBox.GotFocus)).Subscribe(x =>
			{
				SearchPanel.IsVisible = true;
			}));
		}
	}

	private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (!AssociatedObject.IsPointerOver)
		{
			SearchPanel.IsVisible = false;
		}
	}
}