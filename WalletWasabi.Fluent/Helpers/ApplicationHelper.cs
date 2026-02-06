using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;

namespace WalletWasabi.Fluent.Helpers;

public class ApplicationHelper
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(0.2);

	public static ApplicationHelper Instance { get; } = new();

	public static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

	public static IFocusManager? FocusManager => GetTopLevel()?.FocusManager;

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

	public static async Task<string> GetTextAsync()
	{
		if (GetClipboard() is { } clipboard)
		{
			return await Dispatcher.UIThread.InvokeAsync(async () =>
			{
				try
				{
					var content = await clipboard.TryGetTextAsync();
					return content ?? "";
				}
				catch (InvalidCastException)
				{
					return "";
				}
			});
		}

		return "";
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

	public static IObservable<string?> ClipboardTextChanged(IScheduler? scheduler = default) => Observable.Timer(PollingInterval, scheduler ?? Scheduler.Default)
		.Repeat()
		.Select(_ => Observable.FromAsync(GetTextAsync, RxApp.MainThreadScheduler))
		.Merge(1)
		.DistinctUntilChanged();

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

	private static TopLevel? GetTopLevel()
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
		{
			return window;
		}

		if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
		{
			return TopLevel.GetTopLevel(mainView);
		}

		return null;
	}
}
