using NBitcoin;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Services
{
	public class SingleInstanceChecker : IDisposable
	{
		/// <summary>Unique prefix for global mutex name.</summary>
		private const string MutexString = "WalletWasabiSingleInstance";

		/// <summary>Name of system-wide mutex.</summary>
		private readonly string _lockName;

		private bool _disposedValue;

		/// <summary>
		/// Creates a new instance of the object where lock name is based on <paramref name="network"/> name. 
		/// </summary>
		/// <param name="network">Bitcoin network selected when Wasabi Wallet was started.</param>
		public SingleInstanceChecker(Network network) : this(network, network.ToString())
		{
		}

		/// <summary>
		/// Use this constructor only for testing.
		/// </summary>
		/// <param name="network">Bitcoin network selected when Wasabi Wallet was started.</param>
		/// <param name="lockName">Name of system-wide mutex.</param>
		public SingleInstanceChecker(Network network, string lockName)
		{
			Network = network;
			_lockName = $"{MutexString}-{lockName}";
		}

		private IDisposable? SingleApplicationLockHolder { get; set; }
		private Network Network { get; }

		public async Task CheckAsync()
		{
			if (_disposedValue)
			{
				throw new ObjectDisposedException(nameof(SingleInstanceChecker));
			}

			// The disposal of this mutex handled by AsyncMutex.WaitForAllMutexToCloseAsync().
			var mutex = new AsyncMutex(_lockName);
			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
				SingleApplicationLockHolder = await mutex.LockAsync(cts.Token).ConfigureAwait(false);
			}
			catch (IOException ex)
			{
				throw new InvalidOperationException($"Wasabi is already running on {Network}!", ex);
			}
		}

		/// <summary>
		/// <list type="bullet">
		/// <item>Unmanaged resources need to be released regardless of the value of the <paramref name="disposing"/> parameter.</item>
		/// <item>Managed resources need to be released if the value of <paramref name="disposing"/> is <c>true</c>.</item>
		/// </list>
		/// </summary>
		/// <param name="disposing">
		/// Indicates whether the method call comes from a <see cref="Dispose()"/> method
		/// (its value is <c>true</c>) or from a finalizer (its value is <c>false</c>).
		/// </param>
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

		/// <summary>
		/// Do not change this code.
		/// </summary>
		public void Dispose()
		{
			// Dispose of unmanaged resources.
			Dispose(true);

			// Suppress finalization.
			GC.SuppressFinalize(this);
		}
	}
}
