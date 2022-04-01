using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Helpers;

public static class ApplicationUtils
{
	public static IObservable<bool> IsMainWindowActive => GetIsMainWindowActive();

	private static IObservable<bool> GetIsMainWindowActive()
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

	public static async Task<string> GetClipboardTextAsync()
	{
		if (Application.Current is {Clipboard: { } clipboard})
		{
			return (string?) await clipboard.GetTextAsync() ?? "";
		}

		return "";
	}
}