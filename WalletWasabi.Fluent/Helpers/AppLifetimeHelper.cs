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
	/// Attempts to restart the app without passing any program arguments.
	/// </summary>
	/// <remarks>
	/// This method is only functional on the published builds
	/// and not on debugging runs.
	/// </remarks>
	public static void Restart()
	{
		StartAppWithArgs();
		ForceShutdown();
	}

	/// <summary>
	/// Restarts the application with shutdown prevention.
	/// </summary>
	/// <remarks>
	/// This method is only functional on the published builds
	/// and not on debugging runs.
	/// </remarks>
	public static void RestartWithShutdownPrevention()
	{
		SafeShutdown(restart: true);
	}

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
	/// Attempts to shutdown the current instance of the app safely with shutdown prevention.
	/// </summary>
	public static void SafeShutdown(bool restart = false)
	{
		if (Application.Current?.DataContext is ApplicationViewModel app)
		{
			app.Shutdown(restart);
		}
	}

	/// <summary>
	/// Attempts to shutdown the current instance of the app safely without shutdown prevention.
	/// </summary>
	public static void ForceShutdown()
	{
		(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
	}
}
