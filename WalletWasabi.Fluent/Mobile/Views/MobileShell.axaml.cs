using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Settings;

namespace WalletWasabi.Fluent.Mobile.Views;

public sealed class MobileShell : UserControl
{
	private CompositeDisposable? _subscriptions;
	private IDisposable? _noticeTimer;
	private TopLevel? _topLevel;
	private RoutableViewModel? _lastPage;

	public MobileShell()
	{
		AvaloniaXamlLoader.Load(this);
		DataTemplates.Insert(0, new MobileEntryViewLocator());
		DataTemplates.Insert(0, new MobileFlowViewLocator());
		DataTemplates.Insert(0, new MobileAuthorizationViewLocator());
		AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		_topLevel = TopLevel.GetTopLevel(this);
		if (_topLevel is not null) _topLevel.BackRequested += OnBackRequested;
		BindNavigation();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (_topLevel is not null) _topLevel.BackRequested -= OnBackRequested;
		_topLevel = null;
		_subscriptions?.Dispose();
		_subscriptions = null;
		_lastPage = null;
		ClearNavigationNotice();
		base.OnDetachedFromVisualTree(e);
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_topLevel is not null) BindNavigation();
	}

	private void BindNavigation()
	{
		_subscriptions?.Dispose();
		_subscriptions = new CompositeDisposable();
		_lastPage = null;
		ClearNavigationNotice();
		this.FindControl<Border>("ApplicationNavigation")!.IsVisible = false;
		if (DataContext is not MainViewModel main) return;
		main.IsMobileLayout = true;
		main.WhenAnyValue(x => x.MainScreen.CurrentPage, x => x.DialogScreen.CurrentPage,
			x => x.FullScreen.CurrentPage, x => x.CompactDialogScreen.CurrentPage)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => UpdatePresentation(main))
			.DisposeWith(_subscriptions);
	}

	private static MobileShellLayer ActiveLayer(MainViewModel main) => MobileShellNavigation.GetTopLayer(
		main.FullScreen.CurrentPage is not null,
		main.DialogScreen.CurrentPage is not null,
		main.CompactDialogScreen.CurrentPage is not null);

	private static RoutableViewModel? ActivePage(MainViewModel main) => ActiveLayer(main) switch
	{
		MobileShellLayer.CompactDialog => main.CompactDialogScreen.CurrentPage,
		MobileShellLayer.Dialog => main.DialogScreen.CurrentPage,
		MobileShellLayer.FullScreen => main.FullScreen.CurrentPage,
		_ => main.MainScreen.CurrentPage
	};

	private ContentControl Host(MobileShellLayer layer) => this.FindControl<ContentControl>(layer switch
	{
		MobileShellLayer.CompactDialog => "CompactContent",
		MobileShellLayer.Dialog => "DialogContent",
		MobileShellLayer.FullScreen => "FullContent",
		_ => "MainContent"
	})!;

	private void UpdatePresentation(MainViewModel main)
	{
		if (!ReferenceEquals(DataContext, main)) return;
		var layer = ActiveLayer(main);
		this.FindControl<Border>("ApplicationNavigation")!.IsVisible = layer == MobileShellLayer.Main &&
			MobileShellNavigation.ShowApplicationNavigation(main.MainScreen.CurrentPage?.GetType());

		// Keep the original route instances alive, but do not expose underlying
		// wallet content to input or accessibility while an authorization is on top.
		foreach (var candidate in Enum.GetValues<MobileShellLayer>())
		{
			var host = Host(candidate);
			host.IsVisible = candidate == layer;
			host.IsEnabled = candidate == layer;
		}

		var page = ActivePage(main);
		if (ReferenceEquals(page, _lastPage)) return;
		_lastPage = page;
		ClearNavigationNotice();
		var activeHost = Host(layer);
		Dispatcher.UIThread.Post(() =>
		{
			// A queued focus request may outlive a DataContext replacement.
			if (_topLevel is null || !ReferenceEquals(DataContext, main) || !ReferenceEquals(page, ActivePage(main))) return;
			activeHost.Focus();
		}, DispatcherPriority.Background);
	}

	private void OnBackRequested(object? sender, RoutedEventArgs e)
	{
		if (HandleBack()) e.Handled = true;
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if ((e.Key is Key.Escape or Key.BrowserBack) && HandleBack()) e.Handled = true;
	}

	private bool HandleBack()
	{
		if (DataContext is not MainViewModel main || ActivePage(main) is not { } page) return false;
		var layer = ActiveLayer(main);
		if (page.IsBusy || Host(layer).GetVisualDescendants().OfType<MobilePage>().Any(x => x.IsBusy))
		{
			ShowNavigationNotice();
			return true;
		}
		// Settings.CancelCommand resets preferences; it must never implement Back.
		if (page is SettingsPageViewModel)
		{
			Execute(layer == MobileShellLayer.Main ? main.NavigateToMobileWalletsCommand : page.NextCommand);
			return true;
		}
		if (layer != MobileShellLayer.Main)
		{
			if (page.EnableBack && Execute(page.BackCommand)) return true;
			if (page.EnableCancelOnEscape && Execute(page.CancelCommand)) return true;
			main.ShowDialogAlert();
			ShowNavigationNotice();
			return true;
		}
		var walletView = Host(MobileShellLayer.Main).GetVisualDescendants().OfType<MobileWalletView>().FirstOrDefault();
		if (walletView?.FindControl<Grid>("Root")?.DataContext is MobileWalletViewModel wallet && wallet.Section != "home")
		{
			wallet.Navigate(wallet.Section == "transaction" ? "history" : "home");
			return true;
		}
		return page.EnableBack && Execute(page.BackCommand);
	}

	private void ShowNavigationNotice()
	{
		_noticeTimer?.Dispose();
		var notice = this.FindControl<Border>("NavigationNotice")!;
		notice.IsVisible = true;
		_noticeTimer = DispatcherTimer.RunOnce(() => notice.IsVisible = false, TimeSpan.FromSeconds(3));
	}

	private void ClearNavigationNotice()
	{
		_noticeTimer?.Dispose();
		_noticeTimer = null;
		this.FindControl<Border>("NavigationNotice")!.IsVisible = false;
	}

	private static bool Execute(ICommand? command)
	{
		if (command?.CanExecute(null) != true) return false;
		command.Execute(null);
		return true;
	}
}
