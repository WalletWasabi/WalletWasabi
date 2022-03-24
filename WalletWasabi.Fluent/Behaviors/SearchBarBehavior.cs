using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class SearchBarBehavior : DisposingBehavior<UserControl>
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

	protected override void OnAttached(CompositeDisposable disposables)
	{
		Dispatcher.UIThread.Post(() =>
		{
			if (AssociatedObject.GetVisualRoot() is TopLevel topLevel)
			{
				topLevel.AddHandler(InputElement.PointerPressedEvent, Handler, RoutingStrategies.Tunnel);

				disposables.Add(Disposable.Create(()=>topLevel.RemoveHandler(InputElement.PointerPressedEvent, Handler)));
			}

			var searchBox = SearchBox;
			var searchPanel = SearchPanel;

			disposables.Add(Observable.FromEventPattern(searchBox, nameof(searchBox.GotFocus)).Subscribe(x =>
			{
				SearchPanel.IsVisible = true;
			}));

		});
	}

	private void Handler(object? sender, PointerPressedEventArgs e)
	{
		if (!AssociatedObject.IsPointerOver)
		{
			SearchPanel.IsVisible = false;
		}
	}
}