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
		private string FullName { get; set; }

		private Mutex Mutex { get; set; }
		private AsyncLock AsyncLock { get; set; }
		private Thread MutexThread { get; set; }

		/// <summary>
		/// string: mutex name
		/// </summary>
		private static Dictionary<string, AsyncMutex> AsyncMutexes { get; } = new Dictionary<string, AsyncMutex>();

		private static object AsyncMutexesLock { get; } = new object();

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
			FullName = $"Global\\4AA0E5A2-A94F-4B92-B962-F2BBC7A68323-{name}";
			Mutex = null;
			MutexThread = null;

			// If we already have an asynclock with this fullname then just use it and don't create a new one.
			lock (AsyncMutexesLock)
			{
				if (AsyncMutexes.TryGetValue(FullName, out AsyncMutex asyncMutex))
				{
					AsyncLock = asyncMutex.AsyncLock;
				}
				else
				{
					AsyncLock = new AsyncLock();

					AsyncMutexes.Add(FullName, this);
				}
			}
		}

		private int _command;
		private ManualResetEvent ToDo { get; } = new ManualResetEvent(false);
		private ManualResetEvent Done { get; } = new ManualResetEvent(false);

		private Exception LatestHoldLockException { get; set; }
		private object LatestHoldLockExceptionLock { get; } = new object();

		private void HoldLock()
		{
			while (true)
			{
				try
				{
					ToDo.WaitOne();

					if (Interlocked.CompareExchange(ref _command, 0, 1) == 1)
					{
						Mutex = new Mutex(initiallyOwned: true, FullName, out bool createdNew);
						if (createdNew)
						{
							continue;
						}
						else
						{
							bool acquired = Mutex.WaitOne(90000);

							if (acquired)
							{
								continue;
							}
						}
					}
					else if (Interlocked.CompareExchange(ref _command, 0, 2) == 2)
					{
						Mutex?.ReleaseMutex();
						Mutex?.Dispose();
						Mutex = null;
						return;
					}

					throw new NotImplementedException();
				}
				catch (Exception ex)
				{
					lock (LatestHoldLockExceptionLock)
					{
						LatestHoldLockException = ex;
					}
				}
				finally
				{
					ToDo.Reset();
					Done.Set();
				}
			}
		}

		private async Task SetCommandAsync(int command, CancellationToken cancellationToken, int pollInterval)
		{
			Interlocked.Exchange(ref _command, command);
			lock (LatestHoldLockExceptionLock)
			{
				LatestHoldLockException = null;
			}
			Done.Reset();
			ToDo.Set();
			while (!Done.WaitOne(1))
			{
				await Task.Delay(pollInterval, cancellationToken);
			}
			lock (LatestHoldLockExceptionLock)
			{
				if (LatestHoldLockException != null)
				{
					throw LatestHoldLockException;
				}
			}
		}

		private void SetCommand(int command)
		{
			Interlocked.Exchange(ref _command, command);
			ToDo.Set();
		}

		public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default, int pollInterval = 100)
		{
			Exception inner = null;

			try
			{
				await AsyncLock.LockAsync(cancellationToken);

				MutexThread = new Thread(new ThreadStart(() =>
				{
					HoldLock();
				}));
				MutexThread.Start();
				await SetCommandAsync(1, cancellationToken, pollInterval);

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
			SetCommand(2);
			MutexThread?.Join();
			AsyncLock?.ReleaseLock();
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
						try
						{
							AsyncMutex.ReleaseLock();
						}
						catch (Exception ex)
						{
							Logger.LogWarning<AsyncMutex>(ex);
						}
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
