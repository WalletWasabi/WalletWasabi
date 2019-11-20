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
		public WasabiClient WasabiClient { get; }

		public UpdateStatus UpdateStatus { get; private set; }

		public event EventHandler<UpdateStatus> UpdateStatusChanged;

		public UpdateChecker(TimeSpan period, WasabiClient client) : base(period)
		{
			WasabiClient = Guard.NotNull(nameof(client), client);
			UpdateStatus = new UpdateStatus(true, true);
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			var updateStatus = await WasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
			if (updateStatus != UpdateStatus)
			{
				UpdateStatus = updateStatus;
				UpdateStatusChanged?.Invoke(this, updateStatus);
			}
		}
	}
}
