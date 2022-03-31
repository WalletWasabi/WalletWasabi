using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Helpers;

public static class MainWindowEvents
{
	public static IObservable<bool> IsActiveChanged => GetIsActiveObs();

	private static IObservable<bool> GetIsActiveObs()
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
}