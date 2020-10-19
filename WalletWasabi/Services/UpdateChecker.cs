using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class UpdateChecker : PeriodicRunner
	{
		public UpdateChecker(TimeSpan period, WasabiSynchronizer synchronizer) : base(period)
		{
			Synchronizer = synchronizer;
			WasabiClient = synchronizer.WasabiClient;
			UpdateStatus = new UpdateStatus(true, true, new Version(), 0);

			Synchronizer.PropertyChanged += Synchronizer_PropertyChanged;
		}

		public event EventHandler<UpdateStatus>? UpdateStatusChanged;

		public WasabiClient WasabiClient { get; }

		public WasabiSynchronizer Synchronizer { get; }

		public UpdateStatus UpdateStatus { get; set; }

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
			var newUpdateStatus = await WasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
			if (newUpdateStatus != UpdateStatus)
			{
				UpdateStatus = newUpdateStatus;
				UpdateStatusChanged?.Invoke(this, newUpdateStatus);
			}
		}

		public override void Dispose()
		{
			Synchronizer.PropertyChanged -= Synchronizer_PropertyChanged;
			base.Dispose();
		}
	}
}
