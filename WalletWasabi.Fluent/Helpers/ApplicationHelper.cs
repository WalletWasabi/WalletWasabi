using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;

namespace WalletWasabi.Fluent.Helpers;

public static class ApplicationHelper
{
	public static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

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
		if (Application.Current?.Clipboard == null)
		{
			return Observable.Return<string?>(null);
		}

		return Observable.Interval(TimeSpan.FromSeconds(0.2), scheduler ?? Scheduler.Default)
			.Select(_ => Observable.FromAsync(async () =>
			{
				var text = await Application.Current.Clipboard.GetTextAsync();
				return text;
			}, RxApp.MainThreadScheduler))
			.Merge(1)
			.DistinctUntilChanged();
	}
}
