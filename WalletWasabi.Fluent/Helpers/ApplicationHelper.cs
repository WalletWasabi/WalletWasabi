using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;

namespace WalletWasabi.Fluent.Helpers;

public static class ApplicationHelper
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(0.2);

	public static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

	public static async Task<string> GetTextAsync()
	{
		if (GetClipboard() is { } clipboard)
		{
			return await Dispatcher.UIThread.InvokeAsync(async () => await clipboard.GetTextAsync() ?? "");
		}

		return await Task.FromResult("");
	}

	public static async Task SetTextAsync(string? text)
	{
		if (GetClipboard() is { } clipboard && text is not null)
		{
			await Dispatcher.UIThread.InvokeAsync(async () => await clipboard.SetTextAsync(text));
		}
	}

	public static async Task ClearAsync()
	{
		if (GetClipboard() is { } clipboard)
		{
			await Dispatcher.UIThread.InvokeAsync(async () => await clipboard.ClearAsync());
		}
	}

	private static IClipboard? GetClipboard()
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
		return Observable.Timer(PollingInterval, scheduler ?? Scheduler.Default)
			.Repeat()
			.Select(_ => Observable.FromAsync(GetTextAsync, RxApp.MainThreadScheduler))
			.Merge(1)
			.DistinctUntilChanged();
	}
}
