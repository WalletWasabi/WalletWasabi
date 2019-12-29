using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class UpdateChecker : PeriodicRunner
	{
		private UpdateStatus _updateStatus;

		public WasabiClient WasabiClient { get; }

		public UpdateStatus UpdateStatus
		{
			get => _updateStatus;
			private set
			{
				if (value != _updateStatus)
				{
					_updateStatus = value;
					UpdateStatusChanged?.Invoke(this, value);
				}
			}
		}

		public event EventHandler<UpdateStatus> UpdateStatusChanged;

		public UpdateChecker(TimeSpan period, WasabiClient client) : base(period)
		{
			WasabiClient = Guard.NotNull(nameof(client), client);
			UpdateStatus = new UpdateStatus(true, true, new Version(0, 0));
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			UpdateStatus = await WasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
		}
	}
}
