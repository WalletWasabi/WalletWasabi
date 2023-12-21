using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Views;

namespace WalletWasabi.Fluent.Helpers;

public static class ApplicationHelper
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(0.2);
	public static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

	public static IClipboard? Clipboard => GetClipboard();

	public static IClipboard? GetClipboard()
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
		{
			return window.Clipboard;
		}

		if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
		{
			var visualRoot = mainView.GetVisualRoot();
			if (visualRoot is TopLevel topLevel)
			{
				return topLevel.Clipboard;
			}
		}

		return null;
	}

	public static IObservable<bool> MainWindowActivated
	{
		get
		{
			if (MainWindow is not { } mainWindow)
			{
				return Observable.Return(false);
			}

			var activated = Observable
				.FromEventPattern(mainWindow, nameof(Window.Activated))
				.Select(_ => true);

			var deactivated = Observable
				.FromEventPattern(mainWindow, nameof(Window.Deactivated))
				.Select(_ => false);

			return activated.Merge(deactivated);
		}
	}

	public static IObservable<string?> ClipboardTextChanged(IScheduler? scheduler = default)
	{
		if (Clipboard == null)
		{
			return Observable.Return<string?>(null);
		}

		return Observable.Timer(PollingInterval, scheduler ?? Scheduler.Default)
			.Repeat()
			.Select(_ => Observable.FromAsync(() => Clipboard.GetTextAsync(), RxApp.MainThreadScheduler))
			.Merge(1)
			.DistinctUntilChanged();
	}
}
