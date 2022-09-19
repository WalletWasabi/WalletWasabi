using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Microservices;

namespace WalletWasabi.Fluent.Helpers;

/// <summary>
/// Helper methods for Application lifetime related functions.
/// </summary>
public static class AppLifetimeHelper
{
	/// <summary>
	/// Attempts to start a new instance of the app with optional program arguments
	/// </summary>
	/// <remarks>
	/// This method is only functional on the published builds
	/// and not on debugging runs.
	/// </remarks>
	/// <param name="args">The program arguments to pass to the new instance.</param>
	public static void StartAppWithArgs(string args = "")
	{
		var path = Process.GetCurrentProcess().MainModule?.FileName;

		if (string.IsNullOrEmpty(path))
		{
			throw new InvalidOperationException($"Invalid path: '{path}'");
		}

		var startInfo = ProcessStartInfoFactory.Make(path, args);
		using var p = Process.Start(startInfo);
	}

	/// <summary>
	/// Shuts down the application safely, optionally shutdown prevention and restart can be requested.
	/// </summary>
	/// <remarks>
	/// This method is only functional on the published builds
	/// and not on debugging runs.
	/// </remarks>
	/// <param name="withShutdownPrevention">Enabled the shutdown prevention, so a dialog will pop until the shutdown can be done safely.</param>
	/// <param name="restart">If true, the application will restart after shutdown.</param>
	public static void Shutdown(bool withShutdownPrevention = true, bool restart = false)
	{
		switch ((withShutdownPrevention, restart))
		{
			case (true, true):
			case (true, false):
				(Application.Current?.DataContext as ApplicationViewModel)?.Shutdown(restart);
				break;

			case (false, true):
				StartAppWithArgs();
				(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
				break;

			case (false, false):
				(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
				break;
		}
	}
}
