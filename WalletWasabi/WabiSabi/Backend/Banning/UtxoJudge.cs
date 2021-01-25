using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Banning
{
	/// <summary>
	/// Judge sends UTXOs to prison.
	/// </summary>
	public class UtxoJudge : PeriodicRunner
	{
		/// <param name="period">How often to make judgements.</param>
		public UtxoJudge(TimeSpan period, string prisonFilePath) : base(period)
		{
			Prison = UtxoPrison.FromFileOrEmpty(prisonFilePath);
			var inmateCount = Prison.CountInmates();
			if (inmateCount != 0)
			{
				Logger.LogInfo($"{inmateCount} UTXOs are found in prison. {Prison.CountInmates(Punishment.Banned)} are banned {Prison.CountInmates(Punishment.Noted)} are noted.");
			}
		}

		public UtxoPrison Prison { get; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			var change = false;

			if (change)
			{
				await Prison.ToFileAsync().ConfigureAwait(false);
			}
		}
	}
}
