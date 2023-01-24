using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;

namespace WalletWasabi.Fluent.Helpers;

public static class ApplicationHelper
{
	public static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

	public static IObservable<string> ClipboardTextChanged(IScheduler? scheduler = default)
	{
		return Observable.Interval(TimeSpan.FromSeconds(0.2), scheduler ?? Scheduler.Default)
			.SelectMany(
				_ => Application.Current?.Clipboard?.GetTextAsync()
					.ToObservable() ?? Observable.Return<string?>(null)
					.WhereNotNull())
			.DistinctUntilChanged();
	}
}
