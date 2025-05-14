using Avalonia;
using Avalonia.ReactiveUI;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using WalletWasabi.Fluent.CrashReport;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Fluent.Desktop.Extensions;
using System.Net.Sockets;
using System.Collections.ObjectModel;
using WalletWasabi.Daemon;
using LogLevel = WalletWasabi.Logging.LogLevel;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Desktop;

public class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static async Task<int> Main(string[] args)
	{
		// Crash reporting must be before the "single instance checking".
		Logger.InitializeDefaults(Path.Combine(Config.DataDir, "Logs.txt"), LogLevel.Info);
		try
		{
			if (CrashReporter.TryGetExceptionFromCliArgs(args, out var exceptionToShow))
			{
				// Show the exception.
				BuildCrashReporterApp(exceptionToShow).StartWithClassicDesktopLifetime(args);
				return 1;
			}
		}
		catch (Exception ex)
		{
			// If anything happens here just log it and exit.
			Logger.LogCritical(ex);
			return 1;
		}

		try
		{
			return await RunAsync(args);
		}
		catch (Exception ex)
		{
			CrashReporter.Invoke(ex);
			Logger.LogCritical(ex);
			return 1;
		}
	}

	private static async Task<int> RunAsync(string[] args)
	{
		var app = WasabiAppBuilder
			.Create("Wasabi GUI", args)
			.EnsureSingleInstance()
			.OnUnhandledExceptions(LogUnhandledException)
			.OnUnobservedTaskExceptions(LogUnobservedTaskException)
			.OnTermination(TerminateApplication)
			.Build();

		var exitCode = await app.RunAsGuiAsync();

		if (app.TerminateService.GracefulCrashException is not null)
		{
			throw app.TerminateService.GracefulCrashException;
		}

		if (exitCode == ExitCode.Ok && app.Global is {Status: {InstallOnClose: true, InstallerFilePath: var installerFilePath}})
		{
			Installer.StartInstallingNewVersion(installerFilePath);
		}

		return (int)exitCode;
	}

	/// <summary>
	/// Do not call this method it should only be called by TerminateService.
	/// </summary>
	private static void TerminateApplication()
	{
		Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Close());
	}

	private static void LogUnobservedTaskException(object? sender, AggregateException e)
	{
		ReadOnlyCollection<Exception> innerExceptions = e.Flatten().InnerExceptions;

		switch (innerExceptions)
		{
			case [SocketException { SocketErrorCode: SocketError.OperationAborted }]:
			// Source of this exception is NBitcoin library.
			case [OperationCanceledException { Message: "The peer has been disconnected" }]:
				// Until https://github.com/MetacoSA/NBitcoin/pull/1089 is resolved.
				Logger.LogTrace(e);
				break;

			default:
				Logger.LogDebug(e);
				break;
		}
	}

	private static void LogUnhandledException(object? sender, Exception e) =>
		Logger.LogWarning(e);

	[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Required to bootstrap Avalonia's Visual Previewer")]
	private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App()).UseReactiveUI().SetupAppBuilder();

	/// <summary>
	/// Sets up and initializes the crash reporting UI.
	/// </summary>
	/// <param name="serializableException">The serializable exception</param>
	private static AppBuilder BuildCrashReporterApp(SerializableException serializableException)
	{
		var result = AppBuilder
			.Configure(() => new CrashReportApp(serializableException))
			.UseReactiveUI();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			result
				.UseWin32()
				.UseSkia();
		}
		else
		{
			result.UsePlatformDetect();
		}

		return result
			.With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
			.With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software }, WmClass = "Wasabi Wallet Crash Report" })
			.With(new AvaloniaNativePlatformOptions { RenderingMode = new[] { AvaloniaNativeRenderingMode.Software } })
			.With(new MacOSPlatformOptions { ShowInDock = true })
			.AfterSetup(_ => ThemeHelper.ApplyTheme(Theme.Dark));
	}
}
