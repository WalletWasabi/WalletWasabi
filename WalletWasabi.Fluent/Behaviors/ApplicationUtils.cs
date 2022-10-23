using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Behaviors;

public static class ApplicationUtils
{
	public static IObservable<bool> MainWindowIsActive()
	{
		if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime app)
		{
			return Observable.Return(true);
		}

		var isActive = Observable
			.FromEventPattern(app.MainWindow, nameof(Window.Activated))
			.Select(_ => true);

		var isInactive = Observable
			.FromEventPattern(app.MainWindow, nameof(Window.Deactivated))
			.Select(_ => false);

		return isActive
			.Merge(isInactive)
			.StartWith(app.MainWindow.IsActive);
	}

	public static IObservable<string> GetClipboard()
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			return clipboard.GetTextAsync().ToObservable().Select(x => x ?? "");
		}

		return Observable.Return("");
	}
}
