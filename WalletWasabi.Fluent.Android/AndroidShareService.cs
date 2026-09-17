using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Avalonia.Threading;
using WalletWasabi.Fluent.Mobile.Services;

namespace WalletWasabi.Fluent.Android;

internal sealed class AndroidShareService(Activity activity) : IMobileShareService
{
	private readonly WeakReference<Activity> _activity = new(activity);
	public Task PresentAsync(string payload, CancellationToken cancellationToken)
	{
		Dispatcher.UIThread.VerifyAccess();
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentException.ThrowIfNullOrWhiteSpace(payload);
		if (!_activity.TryGetTarget(out var host) || host.IsFinishing || host.IsDestroyed)
			throw new InvalidOperationException("No foreground activity can present the sharesheet.");
		using var intent = new Intent(Intent.ActionSend);
		intent.SetType("text/plain");
		intent.PutExtra(Intent.ExtraText, payload);
		using var chooser = Intent.CreateChooser(intent, "Share Bitcoin request");
		host.StartActivity(chooser);
		return Task.CompletedTask;
	}
}
