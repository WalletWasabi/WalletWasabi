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

	public static IObservable<string> ClipBoardTextChanged()
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			return clipboard.GetTextAsync().ToObservable().Select(x => x ?? "");
		}

		return Observable.Return("");
	}

	public static IObservable<string> ClipboardTextChanged => Observable.Interval(TimeSpan.FromSeconds(0.2))
		.SelectMany(_ => Application.Current.Clipboard
			.GetTextAsync()
			.ToObservable()
			.WhereNotNull());
}
