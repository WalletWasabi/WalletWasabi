using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Services
{
	public class SingleInstanceChecker : IDisposable
	{
		private const string MutexString = "WalletWasabiSingleInstance";
		private bool _disposedValue;

		public SingleInstanceChecker(Network network)
		{
			Network = network;
		}

		private IDisposable SingleApplicationLockHolder { get; set; }
		private Network Network { get; }

		public async Task CheckAsync()
		{
			if (_disposedValue)
			{
				throw new ObjectDisposedException(nameof(SingleInstanceChecker));
			}

			// The disposal of this mutex handled by AsyncMutex.WaitForAllMutexToCloseAsync().
			var mutex = new AsyncMutex($"{MutexString}-{Network}");
			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.Zero);
				SingleApplicationLockHolder = await mutex.LockAsync(cts.Token).ConfigureAwait(false);
			}
			catch (IOException ex)
			{
				throw new InvalidOperationException($"Wasabi is already running on {Network}!", ex);
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					SingleApplicationLockHolder?.Dispose();
				}
				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
		}
	}
}
