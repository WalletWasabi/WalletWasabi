using Android.Content;
using WalletWasabi.Logging;
using WalletWasabi.Mobile;

namespace WalletWasabi.Android;

internal static class WalletRuntime
{
	private static readonly SemaphoreSlim Gate = new(1, 1);
	private static TorHost? _tor;
	private static CancellationTokenSource? _stop;
	private static bool _loggingConfigured;
	public static WalletSession? Session { get; private set; }
	public static string? Error { get; private set; }
	public static int Bootstrap => _tor?.Bootstrap ?? 0;
	public static string DataDir(Context context) => Path.Combine(context.FilesDir!.AbsolutePath, "Wasabi");

	public static async Task StartAsync(Context context)
	{
		await Gate.WaitAsync().ConfigureAwait(false);
		try
		{
			if (Session is not null)
			{
				return;
			}
			Error = null;
			_stop = new();
			var dataDir = DataDir(context);
			Directory.CreateDirectory(dataDir);
			if (!_loggingConfigured)
			{
				Logger.Configure(Path.Combine(dataDir, "Logs.txt"), LogLevel.Warning, [LogMode.File]);
				_loggingConfigured = true;
			}
			var settings = MobileSettings.Load(dataDir);
			var session = new WalletSession(dataDir, settings, context.ApplicationInfo!.NativeLibraryDir!);
			Session = session;
			_tor = new();
			await _tor.StartAsync(context, dataDir, settings, _stop.Token).ConfigureAwait(false);
			await session.InitializeAsync(_stop.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error = ex is OperationCanceledException ? "Tor connection timed out. Reopen Wasabi to retry." : ex.Message;
			if (Session is { } session) { await session.DisposeAsync().ConfigureAwait(false); Session = null; }
			if (_tor is { } tor) { await tor.DisposeAsync().ConfigureAwait(false); _tor = null; }
			_stop?.Dispose();
			_stop = null;
		}
		finally { Gate.Release(); }
	}

	public static async Task StopAsync()
	{
		_stop?.Cancel();
		await Gate.WaitAsync().ConfigureAwait(false);
		try
		{
			if (Session is { } session)
			{
				await session.DisposeAsync().ConfigureAwait(false);
				Session = null;
			}
			if (_tor is { } tor) { await tor.DisposeAsync().ConfigureAwait(false); _tor = null; }
			_stop?.Dispose();
			_stop = null;
		}
		finally { Gate.Release(); }
	}
}
