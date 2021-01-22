using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;

namespace WalletWasabi.WabiSabi.Backend.Banning
{
	/// <summary>
	/// Judge sends UTXOs to prison.
	/// </summary>
	public class Judge : PeriodicRunner
	{
		/// <param name="period">How often to make judgements.</param>
		public Judge(TimeSpan period, string prisonFilePath) : base(period)
		{
			Prison = Prison.FromFileOrEmpty(prisonFilePath);
		}

		public Prison Prison { get; }

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
