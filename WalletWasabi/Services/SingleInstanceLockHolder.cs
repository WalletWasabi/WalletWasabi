using System;
using System.Threading;

namespace WalletWasabi.Services
{
	/// <summary>
	/// Object obtained by <see cref="SingleInstanceChecker"/>.
	/// </summary>
	public class SingleInstanceLockHolder : IDisposable
	{
		/// <summary>Creates new instance of the object.</summary>
		/// <param name="mutex">Acquired system-wide mutex.</param>
		public SingleInstanceLockHolder(Mutex mutex)
		{
			Mutex = mutex;
		}

		/// <summary>System-wide mutex.</summary>
		private Mutex Mutex { get; }

		/// <inheritdoc/>
		public void Dispose()
		{
			Mutex.Dispose();
		}
	}
}
