using NBitcoin;
using System;
using System.Threading;

namespace WalletWasabi.Services
{
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
