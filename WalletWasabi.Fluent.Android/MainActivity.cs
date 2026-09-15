using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Android.App;
using Android.Content.PM;
using Android.Util;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using WalletWasabi.Client;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Android;

[Activity(
	Label = "Wasabi Wallet",
	Theme = "@style/MyTheme.NoActionBar",
	Icon = "@drawable/icon",
	MainLauncher = true,
	LaunchMode = LaunchMode.SingleInstance,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
	WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity<App>
{
	private WasabiApplication? _app;

	protected override void OnResume()
	{
		base.OnResume();
		if (Avalonia.Application.Current is { } application)
			WalletWasabi.Fluent.Mobile.Services.MobileSharing.Register(application, new AndroidShareService(this));
	}

	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		// TODO: Crash reporting
		Log.Error("WASABI", "CustomizeAppBuilder");
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
			_app.RunAsyncMobile(afterStarting: () =>
			{
				App.InitializeMobile(_app, builder);
				AppBuilderAndroidExtension.SetupAppBuilder(builder);
			});
		}
		catch (Exception ex)
		{
			Logger.LogCritical(ex);
			Log.Error("WASABI", $"{ex}");
		}
		return base.CustomizeAppBuilder(builder);
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
	private static AppBuilder BuildAvaloniaApp() => AppBuilderAndroidExtension.SetupAppBuilder(AppBuilder.Configure(() => new App()).UseReactiveUI());
}
