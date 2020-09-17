using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Services
{
	public class SystemAwakeChecker : PeriodicRunner
	{
		public SystemAwakeChecker(TimeSpan period, WalletManager walletManager) : base(period)
		{
			WalletManager = walletManager;
		}

		private WalletManager WalletManager { get; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			if (WalletManager.IsAnyCoinJoinInProgress())
			{
				await EnvironmentHelpers.KeepSystemAwakeAsync().ConfigureAwait(false);
			}
		}
	}
}
