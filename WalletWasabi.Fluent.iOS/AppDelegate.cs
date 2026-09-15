using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Avalonia;
using Avalonia.iOS;
using Avalonia.ReactiveUI;
using Foundation;
using WalletWasabi.Client;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.IOS;

[Register("AppDelegate")]
[Preserve(AllMembers = true)]
public class AppDelegate : AvaloniaAppDelegate<App>
{
	private WasabiApplication? _app;

	private static void LogToFile(string msg)
	{
		try
		{
			var logLine = $"[{DateTime.Now:HH:mm:ss.fff}] [AppDelegate] {msg}\n";
			Console.WriteLine($"[WASABI_IOS] [AppDelegate] {msg}");
			var simSharedDir = Environment.GetEnvironmentVariable("SIMULATOR_SHARED_RESOURCES_DIRECTORY") ?? "/tmp";
			var sharedLogFile = System.IO.Path.Combine(simSharedDir, "wasabi_ios.log");
			System.IO.File.AppendAllText(sharedLogFile, logLine);
		}
		catch { }
	}

	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		LogToFile("CustomizeAppBuilder starting...");
		try
		{
			Global.IsTorEnabled = false;
			_app = WasabiAppBuilder
				.Create("Wasabi GUI", System.Array.Empty<string>())
				.EnsureSingleInstance(false)
				.OnUnhandledExceptions(LogUnhandledException)
				.OnUnobservedTaskExceptions(LogUnobservedTaskException)
				.OnTermination(TerminateApplication)
				.Build();
			LogToFile("WasabiAppBuilder built successfully");
			builder = App.InitializeMobile(_app, builder);
			_app.RunAsyncMobile(afterStarting: () => LogToFile("App starting completed"));
		}
		catch (Exception ex)
		{
			Logger.LogCritical(ex);
			LogToFile($"EXCEPTION in CustomizeAppBuilder: {ex}");
		}
		return builder.AfterSetup(_ =>
		{
			if (Avalonia.Application.Current is { } application)
				WalletWasabi.Fluent.Mobile.Services.MobileSharing.Register(application, new IosShareService());
		});
	}

	/// <summary>Do not call this method; it should only be called by TerminateService.</summary>
	private static void TerminateApplication()
	{
		// TODO: integrate mobile termination lifecycle.
	}

	private static void LogUnobservedTaskException(object? sender, AggregateException e)
	{
		ReadOnlyCollection<Exception> innerExceptions = e.Flatten().InnerExceptions;
		switch (innerExceptions)
		{
			case [SocketException { SocketErrorCode: SocketError.OperationAborted }]:
			case [OperationCanceledException { Message: "The peer has been disconnected" }]:
				Logger.LogTrace(e);
				break;
			default:
				Logger.LogDebug(e);
				break;
		}
	}

	private static void LogUnhandledException(object? sender, Exception e) => Logger.LogWarning(e);

	[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Required to bootstrap Avalonia's Visual Previewer")]
	private static AppBuilder BuildAvaloniaApp() => AppBuilderIOSExtension.SetupAppBuilder(AppBuilder.Configure(() => new App()).UseReactiveUI());
}
