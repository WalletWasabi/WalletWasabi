using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using WalletWasabi.Daemon;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Desktop;

public static class WasabiFluentAppBuilder
{
	public static async Task<int> RunAsync(string[] args)
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
}
