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

		private IDisposable SingleApplicationLockHolder { get; set; }

		public async Task CheckAsync()
		{
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.Zero);

			// The disposal of this mutex handled by AsyncMutex.WaitForAllMutexToCloseAsync().
			var mutex = new AsyncMutex(MutexString);
			try
			{
				SingleApplicationLockHolder = await mutex.LockAsync(cts.Token).ConfigureAwait(false);
			}
			catch (IOException)
			{
				throw new InvalidOperationException("Wasabi is already running!");
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
			GC.SuppressFinalize(this);
		}
	}
}
