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
		private int PollInterval { get; }
		private string FullName { get; set; }

		private Mutex Mutex { get; set; }
		private int _isMutexInitialized;

		public AsyncMutex(string name, int pollInterval = 100)
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
			PollInterval = pollInterval;
			FullName = $"Global\\4AA0E5A2-A94F-4B92-B962-F2BBC7A68323-{name}";
			_isMutexInitialized = 0;
		}

		private async Task<bool> TryGetLockAsync(CancellationToken cancellationToken)
		{
			try
			{
				var start = DateTime.Now;
				while (DateTime.Now - start < TimeSpan.FromSeconds(90))
				{
					var res = Interlocked.CompareExchange(ref _isMutexInitialized, 1, 0);
					if (res == 0)
					{
						// We have the local lock.
						return true;
					}
					await Task.Delay(PollInterval, cancellationToken);
				}
			}
			catch (Exception)
			{
				return false;
			}
			return false;
		}

		public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
		{
			Exception inner = null;
			if (await TryGetLockAsync(cancellationToken))
			{
				try
				{
					Mutex = new Mutex(false, FullName, out bool isNewCreated);

					if (!isNewCreated)
					{
						throw new InvalidOperationException($"Mutex with the same name ({ShortName}) already exists, choose another name.");
					}

					// acquire the mutex (or timeout after 60 seconds)
					// will return false if it timed out
					var start = DateTime.Now;
					while (DateTime.Now - start < TimeSpan.FromSeconds(90))
					{
						bool acquired = Mutex.WaitOne(1);

						if (acquired)
						{
							return new Key(this);
						}
						await Task.Delay(PollInterval, cancellationToken);
					}
					// Let it go.
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
					inner = ex;
					// Let it go.
				}

				Mutex?.Dispose();
				Interlocked.Exchange(ref _isMutexInitialized, 0);
			}
			throw new IOException($"Couldn't acquire system wide mutex on {ShortName}", inner);
		}

		private void ReleaseLock()
		{
			Mutex?.Dispose();
			Interlocked.Exchange(ref _isMutexInitialized, 0);
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
