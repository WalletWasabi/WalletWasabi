using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class UpdateChecker : PeriodicRunner
{
	public UpdateChecker(TimeSpan period, WasabiSynchronizer synchronizer) : base(period)
	{
		Synchronizer = synchronizer;
		UpdateStatus = new UpdateStatus(backendCompatible: true, clientUpToDate: true, new Version(), currentBackendMajorVersion: 0, new Version());
		WasabiClient = Synchronizer.HttpClientFactory.SharedWasabiClient;
		Synchronizer.PropertyChanged += Synchronizer_PropertyChanged;
	}

	public event EventHandler<UpdateStatus>? UpdateStatusChanged;

	private LastExceptionTracker ExceptionTracker { get; } = new LastExceptionTracker();
	private WasabiSynchronizer Synchronizer { get; }
	private UpdateStatus UpdateStatus { get; set; }
	public WasabiClient WasabiClient { get; }

	private void Synchronizer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(WasabiSynchronizer.BackendStatus) &&
			Synchronizer.BackendStatus == BackendStatus.Connected)
		{
			// Any time when the synchronizer detects the backend, we immediately check the versions. GUI relies on UpdateStatus changes.
			TriggerRound();
		}
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		try
		{
			var newUpdateStatus = await WasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
			if (newUpdateStatus != UpdateStatus)
			{
				UpdateStatus = newUpdateStatus;
				UpdateStatusChanged?.Invoke(this, newUpdateStatus);
			}
			ExceptionInfo? info = ExceptionTracker.LastException;
			if (info is { })
			{
				Logger.LogInfo($"Exception stopped coming. It came for " +
					$"{(DateTimeOffset.UtcNow - info.FirstAppeared).TotalSeconds} seconds, " +
					$"{info.ExceptionCount} times: {info.Exception.ToTypeMessageString()}");
				ExceptionTracker.Reset();
			}
		}
		catch (HttpRequestException e)
		{
			var info = ExceptionTracker.Process(e);
			if (info.IsFirst)
			{
				Logger.LogWarning(info.Exception);
			}
		}
	}

	public override void Dispose()
	{
		Synchronizer.PropertyChanged -= Synchronizer_PropertyChanged;
		base.Dispose();
	}
}
