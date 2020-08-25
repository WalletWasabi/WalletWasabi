using NBitcoin;
using System;
using System.Threading;

namespace WalletWasabi.Services
{
	/// <summary>
	/// Guarantees that application is run at most once.
	/// </summary>
	/// <remarks>The class does not use async API in order to prevent possible issues with Avalonia which requires main thread to start the GUI.</remarks>
	public class SingleInstanceChecker
	{
		/// <summary>Unique prefix for global mutex name.</summary>
		private const string MutexString = "WalletWasabiSingleInstance";

		public IDisposable TryAcquireLock(Network network)
		{
			// Named mutex represents a system-wide mutex.
			string mutexName = $"{MutexString}-{network}";

			var mutex = new Mutex(initiallyOwned: false, mutexName, out bool createdNew);

			if (createdNew)
			{
				return new SingleInstanceLockHolder(mutex);
			}
			else
			{
				mutex.Dispose();
				throw new InvalidOperationException($"Wasabi is already running on {network}!");
			}
		}
	}
}
