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
	public class UpdateChecker : PeriodicRunner<UpdateStatus>
	{
		public WasabiClient WasabiClient { get; }

		public UpdateChecker(TimeSpan period, WasabiClient client) : base(period, new UpdateStatus(true, true))
		{
			WasabiClient = Guard.NotNull(nameof(client), client);
		}

		public override async Task<UpdateStatus> ActionAsync(CancellationToken cancel)
		{
			return await WasabiClient.CheckUpdatesAsync(cancel).ConfigureAwait(false);
		}
	}
}
