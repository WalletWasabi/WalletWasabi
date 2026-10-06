using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using WalletWasabi.Mobile;

namespace WalletWasabi.Android;

[Service(Name = "io.wasabiwallet.android.WalletService", Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class WalletService : Service
{
	private const string Channel = "wallet_sync";
	private const int NotificationId = 73;
	private bool _stopping;
	private PowerManager.WakeLock? _wake;
	private System.Threading.Timer? _workTimer;
	public override IBinder? OnBind(Intent? intent) => null;

	public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		{
			var manager = (NotificationManager)GetSystemService(NotificationService)!;
			manager.CreateNotificationChannel(new NotificationChannel(Channel, "Wallet synchronization", NotificationImportance.Low));
		}
		var pending = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
		var builder = OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(this, Channel) : new Notification.Builder(this);
		var notification = builder.SetSmallIcon(Resource.Drawable.wasabi_icon)!
			.SetContentTitle("Wasabi Wallet")!.SetContentText("Synchronizing privately")!
			.SetContentIntent(pending)!.SetOngoing(true)!.SetVisibility(NotificationVisibility.Secret)!.Build();
		if (OperatingSystem.IsAndroidVersionAtLeast(29))
		{
			StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
		}
		else { StartForeground(NotificationId, notification); }
		if (_wake is null)
		{
			_wake = ((PowerManager)GetSystemService(PowerService)!).NewWakeLock(WakeLockFlags.Partial, "Wasabi:WalletWork");
			_wake!.SetReferenceCounted(false);
			_workTimer = new System.Threading.Timer(_ =>
			{
				try
				{
					if (_stopping || _wake is not { } wake) { return; }
					var session = WalletRuntime.Session;
					if (WalletRuntime.Snapshot.Lifecycle is RuntimeLifecycle.Starting or RuntimeLifecycle.Reconnecting
						|| session is not null && (!session.IsReady || session.IsMixing
							|| session.Global.WalletManager.GetWallets().Any(w => !w.Loaded || w.KeyManager.GetBestHeight() < session.Global.FilterHeaders.TipHeight)))
					{
						wake.Acquire(10 * 60 * 1000);
					}
					else if (wake.IsHeld) { wake.Release(); }
					if (!WalletRuntime.InterfaceForeground && session is { IsReady: true, IsMixing: false }
						&& session.Global.FilterHeaders.HashesLeft == 0
						&& session.Global.WalletManager.GetWallets().All(w => w.Loaded && w.KeyManager.GetBestHeight() >= session.Global.FilterHeaders.TipHeight))
					{ new Handler(Looper.MainLooper!).Post(Shutdown); }
				}
				catch (Java.Lang.Exception) { }
				catch (ObjectDisposedException) { }
			}, null, 1000, 30_000);
		}
		_ = WalletRuntime.StartAsync(ApplicationContext!);
		return StartCommandResult.NotSticky;
	}

	public override void OnTimeout(int startId, ForegroundService foregroundServiceType) => Shutdown();

	private async void Shutdown()
	{
		if (_stopping) { return; }
		_stopping = true;
		_workTimer?.Dispose();
		if (_wake is { } wake)
		{
			_wake = null;
			if (wake.IsHeld) { wake.Release(); }
			wake.Dispose();
		}
		StopSelf();
		try { await WalletRuntime.StopAsync().ConfigureAwait(false); }
		catch (Exception) { /* The foreground service is already stopped; private state remains recoverable. */ }
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		Shutdown();
	}
}
