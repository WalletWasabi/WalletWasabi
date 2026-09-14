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
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Mobile.Views;

public sealed class MobileShell : UserControl
{
	private CompositeDisposable? _subscriptions;
	private TopLevel? _topLevel;
	private RoutableViewModel? _lastPage;
	public MobileShell()
	{
		AvaloniaXamlLoader.Load(this);
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
		_subscriptions?.Dispose(); _subscriptions = null; _lastPage = null;
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
		if (DataContext is not MainViewModel main) return;
		main.IsMobileLayout = true;
		main.WhenAnyValue(x => x.MainScreen.CurrentPage, x => x.DialogScreen.CurrentPage, x => x.FullScreen.CurrentPage, x => x.CompactDialogScreen.CurrentPage)
			.ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => UpdatePresentation(main)).DisposeWith(_subscriptions);
	}
	private static RoutableViewModel? ActivePage(MainViewModel main) => main.CompactDialogScreen.CurrentPage ?? main.DialogScreen.CurrentPage ?? main.FullScreen.CurrentPage ?? main.MainScreen.CurrentPage;
	private void UpdatePresentation(MainViewModel main)
	{
		this.FindControl<Border>("ApplicationNavigation")!.IsVisible = main.MainScreen.CurrentPage is not WalletViewModel;
		this.FindControl<ContentControl>("FullContent")!.IsEnabled = main.DialogScreen.CurrentPage is null && main.CompactDialogScreen.CurrentPage is null;
		this.FindControl<ContentControl>("DialogContent")!.IsEnabled = main.CompactDialogScreen.CurrentPage is null;
		var page = ActivePage(main);
		if (ReferenceEquals(page, _lastPage)) return;
		_lastPage = page;
		var targetName = main.CompactDialogScreen.CurrentPage is not null ? "CompactContent" : main.DialogScreen.CurrentPage is not null ? "DialogContent" : main.FullScreen.CurrentPage is not null ? "FullContent" : "MainContent";
		Dispatcher.UIThread.Post(() =>
		{
			if (_topLevel is null || !ReferenceEquals(page, ActivePage(main))) return;
			this.FindControl<ContentControl>(targetName)?.Focus();
		}, DispatcherPriority.Background);
	}
	private void OnBackRequested(object? sender, RoutedEventArgs e) { if (HandleBack()) e.Handled = true; }
	private void OnKeyDown(object? sender, KeyEventArgs e) { if (e.Key is Key.Escape or Key.BrowserBack && HandleBack()) e.Handled = true; }
	private bool HandleBack()
	{
		if (DataContext is not MainViewModel main || ActivePage(main) is not { } page) return false;
		if (page.IsBusy) return true;
		// Settings.CancelCommand is reset-to-default, not dismissal.
		if (page is SettingsPageViewModel) { Execute(page.NextCommand); return true; }
		if (main.IsDialogOpen())
		{
			if (page.EnableBack && Execute(page.BackCommand)) return true;
			if (page.EnableCancelOnEscape && Execute(page.CancelCommand)) return true;
			main.ShowDialogAlert();
			return true;
		}
		var walletView = this.FindControl<ContentControl>("MainContent")!.GetVisualDescendants().OfType<MobileWalletView>().FirstOrDefault();
		if (walletView?.FindControl<Grid>("Root")?.DataContext is MobileWalletViewModel wallet && wallet.Section != "home")
		{
			wallet.Navigate(wallet.Section == "transaction" ? "history" : "home");
			return true;
		}
		return page.EnableBack && Execute(page.BackCommand);
	}
	private static bool Execute(ICommand? command)
	{
		if (command?.CanExecute(null) != true) return false;
		command.Execute(null); return true;
	}
}
