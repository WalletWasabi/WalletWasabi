using Medallion.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace Nito.AsyncEx
{
	public class AsyncMutex
	{
		private string ShortName { get; set; }
		private SystemDistributedLock SystemDistributedLock { get; set; }
		private IDisposable SystemDistributedLockDisposable { get; set; }

		public AsyncMutex(string name)
		{
			// https://docs.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=netframework-4.8
			// On a server that is running Terminal Services, a named system mutex can have two levels of visibility.
			// If its name begins with the prefix "Global\", the mutex is visible in all terminal server sessions.
			// If its name begins with the prefix "Local\", the mutex is visible only in the terminal server session where it was created.
			// In that case, a separate mutex with the same name can exist in each of the other terminal server sessions on the server.
			// If you do not specify a prefix when you create a named mutex, it takes the prefix "Local\".
			// Within a terminal server session, two mutexes whose names differ only by their prefixes are separate mutexes,
			// and both are visible to all processes in the terminal server session.
			// That is, the prefix names "Global\" and "Local\" describe the scope of the mutex name relative to terminal server sessions, not relative to processes.
			ShortName = name;
			SystemDistributedLock = new SystemDistributedLock($"4AA0E5A2-A94F-4B92-B962-F2BBC7A68323-{name}");
			SystemDistributedLockDisposable = null;
		}

		public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default, int pollInterval = 100)
		{
			Exception inner = null;

			try
			{
				SystemDistributedLockDisposable = await SystemDistributedLock.AcquireAsync(cancellationToken: cancellationToken);
				return new Key(this);
			}
			catch (TaskCanceledException)
			{
				// Let it go.
			}
			catch (AbandonedMutexException)
			{
				// abandoned mutexes are still acquired, we just need
				// to handle the exception and treat it as acquisition
				Logger.LogWarning($"AbandonedMutexException in {ShortName}", nameof(AsyncMutex));
				return new Key(this);
			}
			catch (Exception ex)
			{
				Logger.LogError($"{ex.ToTypeMessageString()} in {ShortName}", nameof(AsyncMutex));
				inner = ex;
				// Let it go.
			}

			ReleaseLock();

			throw new IOException($"Couldn't acquire system wide mutex on {ShortName}", inner);
		}

		private void ReleaseLock()
		{
			SystemDistributedLockDisposable?.Dispose();
			SystemDistributedLockDisposable = null;
		}

		/// <summary>
		/// The disposable which releases the mutex.
		/// </summary>
		private sealed class Key : IDisposable
		{
			private AsyncMutex AsyncMutex { get; set; }

			/// <summary>
			/// Creates the key for a mutex.
			/// </summary>
			/// <param name="asyncMutex">The mutex to release. May not be <c>null</c>.</param>
			public Key(AsyncMutex asyncMutex)
			{
				AsyncMutex = asyncMutex;
			}

			#region IDisposable Support

			private volatile bool _disposedValue = false; // To detect redundant calls

			private void Dispose(bool disposing)
			{
				if (!_disposedValue)
				{
					if (disposing)
					{
						AsyncMutex?.ReleaseLock();
					}

					AsyncMutex = null;

					_disposedValue = true;
				}
			}

			// This code added to correctly implement the disposable pattern.
			public void Dispose()
			{
				// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
				Dispose(true);
			}

			#endregion IDisposable Support
		}
	}
}
