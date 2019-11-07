using System;
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

		public event EventHandler<UpdateStatusResult> UpdateChecked;

		public UpdateChecker(TimeSpan period, WasabiClient client) : base(period)
		{
			WasabiClient = Guard.NotNull(nameof(client), client);
		}

		public override async Task ActionAsync(CancellationToken cancel)
		{
			var updates = await WasabiClient.CheckUpdatesAsync(cancel);
			UpdateChecked?.Invoke(this, updates);
		}
	}
}
