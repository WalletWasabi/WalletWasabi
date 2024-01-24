using System.Collections.Generic;

namespace WalletWasabi.Wallets.FilterProcessor;

public record Priority(SyncType SyncType)
{
	public static readonly Comparer<Priority> Comparer = Comparer<Priority>.Create(
		(x, y) =>
		{
			// Turbo and Complete have higher priority over NonTurbo.
			if (x.SyncType != SyncType.NonTurbo && y.SyncType == SyncType.NonTurbo)
			{
				return -1;
			}

			if (y.SyncType != SyncType.NonTurbo && x.SyncType == SyncType.NonTurbo)
			{
				return 1;
			}

			return 0;
		});
}
