using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace Nito.AsyncEx
{
	public class AsyncMutex
	{
		/// <summary>
		/// I did this because enum cannot be Interlocked easily.
		/// </summary>
		public enum AsyncLockStatus
		{
			StatusUninitialized = 0,
			StatusReady = 1,
			StatusAcquiring = 2,
			StatusAcquired = 3,
			StatusReleasing = 4
		}

		private int _status;

		public bool IsQuitPending { get; set; }

		/// <summary>
		/// Short name of the mutex. This string added to the end of the mutex name.
		/// </summary>
		private string ShortName { get; set; }

		/// <summary>
		/// The full name of the named mutex. Global\ included in the string.
		/// </summary>
		private string FullName { get; set; }

		/// <summary>
		/// Mutex for interprocess synchronization.
		/// </summary>
		private Mutex Mutex { get; set; }

		/// <summary>
		/// AsyncLock for local thread synchronization.
		/// </summary>
		private AsyncLock AsyncLock { get; set; }

		/// <summary>
		/// Separate thread for the mutex where it is created and released.
		/// </summary>
		private Thread MutexThread { get; set; }

		private bool IsAlive => MutexThread?.IsAlive is true;

		/// <summary>
		/// Static storage for local mutexes. It can be used to get an already existing AsyncLock by name of the mutex.
		/// </summary>
		private static Dictionary<string, AsyncMutex> AsyncMutexes { get; } = new Dictionary<string, AsyncMutex>();

		private static object AsyncMutexesLock { get; } = new object();

		public static bool IsAny => AsyncMutexes.Any();

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

			// If we already have an asynclock with this fullname then just use it and do not create a new one.
			lock (AsyncMutexesLock)
			{
				if (AsyncMutexes.TryGetValue(FullName, out AsyncMutex asyncMutex))
				{
					// Use the already existing lock.
					AsyncLock = asyncMutex.AsyncLock;
				}
				else
				{
					// Create a new lock.
					AsyncLock = new AsyncLock();

					AsyncMutexes.Add(FullName, this);
				}
			}
			ChangeStatus(AsyncLockStatus.StatusReady, AsyncLockStatus.StatusUninitialized);
		}

		private int _command;
		private ManualResetEvent ToDo { get; } = new ManualResetEvent(false);
		private ManualResetEvent Done { get; } = new ManualResetEvent(false);

		private Exception LatestHoldLockException { get; set; }
		private object LatestHoldLockExceptionLock { get; } = new object();

		/// <summary>
		/// The thread of the Mutex. This whole complex logic is needed because we have serious problems
		/// with releasing the mutex from a separate thread. Mutex.Dispose() silently fails when you try to
		/// release the mutex so you have to use Mutex.ReleaseMutex(). It is not possible to release the mutex
		/// from another thread and this was something hard to achieve in an async/await environment with
		/// multi-platform compatibility. For example Semaphore is not supporting linux.
		/// So to ensure that the mutex is created and released on the same thread we have created
		/// a separate thread and control it from outside.
		/// </summary>
		/// <param name="cancellationTokenObj"></param>
		private void HoldLock(object cancellationTokenObj)
		{
			CancellationToken ct = cancellationTokenObj is CancellationToken token
				? token
				: CancellationToken.None;

			while (true)
			{
				try
				{
					// Wait for until we have something to do. The procedure is to set _command variable then
					// signal with ToDo then wait until the Done is set.
					ToDo.WaitOne();

					if (Interlocked.CompareExchange(ref _command, 0, 1) == 1)
					{
						// Create the mutex and acquire it. InitiallyOwned means that if the mutex is not
						// exists then create it and immediately acquire it.
						Mutex = new Mutex(initiallyOwned: true, FullName, out bool createdNew);
						if (createdNew)
						{
							continue;
						}
						else
						{
							// The mutex already exists so we will try to acquire it.
							DateTime start = DateTime.Now;
							bool acquired = false;

							// Timeout logic.
							while (true)
							{
								if (DateTime.Now - start > TimeSpan.FromSeconds(90))
								{
									throw new TimeoutException("Could not acquire mutex in time.");
								}
								// Block for n ms and try to acquire the mutex. Blocking is not a problem
								// we are on our own thread.
								acquired = Mutex.WaitOne(1000);

								if (acquired)
								{
									break;
								}

								if (ct != CancellationToken.None)
								{
									if (ct.IsCancellationRequested)
									{
										throw new OperationCanceledException();
									}
								}
							}

							if (acquired)
							{
								// Go to finally.
								continue;
							}
							// Let it go and throw the exception...
						}
					}
					else if (Interlocked.CompareExchange(ref _command, 0, 2) == 2)
					{
						// Command 2 is releasing the mutex.
						Mutex?.ReleaseMutex();
						Mutex?.Dispose();
						Mutex = null;
						return; // End of the Thread.
					}

					// If we get here something went wrong.
					throw new NotImplementedException($"{nameof(AsyncMutex)} thread operation failed in {ShortName}.");
				}
				catch (Exception ex)
				{
					// We had an exception so store it and jump to finally.
					lock (LatestHoldLockExceptionLock)
					{
						LatestHoldLockException = ex;
					}
					// Terminate the Thread.
					return;
				}
				finally
				{
					ToDo.Reset();
					Done.Set(); // Indicate that we are ready with the current command.
				}
			}
		}

		/// <summary>
		/// Standard procedure to send command to the mutex thread.
		/// </summary>
		/// <param name="command"></param>
		/// <param name="cancellationToken"></param>
		/// <param name="pollInterval"></param>
		/// <returns></returns>
		private async Task SetCommandAsync(int command, CancellationToken cancellationToken, int pollInterval)
		{
			if (!IsAlive)
			{
				throw new InvalidOperationException("Thread should be alive.");
			}

			Interlocked.Exchange(ref _command, command); // Set the command.
			lock (LatestHoldLockExceptionLock)
			{
				LatestHoldLockException = null;
			}
			Done.Reset(); // Reset the Done.
			ToDo.Set(); // Indicate that there is a new command.
			while (!Done.WaitOne(1))
			{
				// Waiting for Done asynchronously.
				await Task.Delay(pollInterval, cancellationToken);
			}
			lock (LatestHoldLockExceptionLock)
			{
				// If we had an exception then throw it.
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

		private void ChangeStatus(AsyncLockStatus newStatus, AsyncLockStatus expectedPreviousStatus)
		{
			ChangeStatus(newStatus, new[] { expectedPreviousStatus });
		}

		private void ChangeStatus(AsyncLockStatus newStatus, AsyncLockStatus[] expectedPreviousStatuses)
		{
			var prevstatus = Interlocked.Exchange(ref _status, (int)newStatus);

			if (!expectedPreviousStatuses.Contains((AsyncLockStatus)prevstatus))
			{
				throw new InvalidOperationException($"Previous {nameof(AsyncLock)} state was unexpected: prev:{((AsyncLockStatus)prevstatus).ToString()} now:{((AsyncLockStatus)_status).ToString()}.");
			}
		}

		/// <summary>
		/// The Lock mechanism designed for standard using blocks. This lock is thread and interprocess safe.
		/// You can create and use it from anywhere.
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <param name="pollInterval">The frequency of polling the termination of the mutex-thread.</param>
		/// <returns>The IDisposable await-able Task.</returns>
		public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default, int pollInterval = 100)
		{
			Exception inner = null;

			try
			{
				if (IsQuitPending)
				{
					throw new OperationCanceledException($"{nameof(AsyncMutex)}.{nameof(AsyncMutex.LockAsync)} failed because quit is pending on: {ShortName}.");
				}

				// Local lock for thread safety.
				await AsyncLock.LockAsync(cancellationToken);

				if (IsAlive)
				{
					throw new InvalidOperationException("Thread should not be alive.");
				}

				MutexThread = new Thread(new ParameterizedThreadStart(HoldLock)) { Name = $"{nameof(MutexThread)}" };

				MutexThread.Start(cancellationToken);

				ChangeStatus(AsyncLockStatus.StatusAcquiring, AsyncLockStatus.StatusReady);

				// Thread is running from now.

				try
				{
					// Create the mutex and acquire it.
					await SetCommandAsync(1, cancellationToken, pollInterval);
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);

					// If the thread is still alive we must stop it
					StopThread();

					ChangeStatus(AsyncLockStatus.StatusReady, AsyncLockStatus.StatusAcquiring);

					throw ex;
				}

				ChangeStatus(AsyncLockStatus.StatusAcquired, AsyncLockStatus.StatusAcquiring);

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
				Logger.LogWarning($"{nameof(AbandonedMutexException)} in {ShortName}.");
				return new Key(this);
			}
			catch (Exception ex)
			{
				Logger.LogError($"{ex.ToTypeMessageString()} in {ShortName}.");
				inner = ex;
				// Let it go.
			}

			// Release the local lock.
			AsyncLock.ReleaseLock();

			throw new IOException($"Could not acquire system wide mutex on {ShortName}.", inner);
		}

		private void StopThread()
		{
			try
			{
				var start = DateTime.Now;
				while (IsAlive)
				{
					SetCommand(2);
					MutexThread?.Join(TimeSpan.FromSeconds(1));

					if (DateTime.Now - start > TimeSpan.FromSeconds(10))
					{
						throw new TimeoutException($"Could not stop {nameof(MutexThread)}, aborting it.");
					}
				}

				// Successfully stopped the thread.
				return;
			}
			catch (Exception ex)
			{
				// Let it go...
				Logger.LogWarning(ex);
			}

			// Final solution, abort the thread.

			// MutexThread.Abort(); // Not supported on Linux!
		}

		private void ReleaseLock()
		{
			if (!IsAlive)
			{
				throw new InvalidOperationException("Thread should be alive.");
			}

			// On multiply call we will get an exception. This is not a dispose so we can throw here.
			ChangeStatus(AsyncLockStatus.StatusReleasing, AsyncLockStatus.StatusAcquired);

			StopThread();

			ChangeStatus(AsyncLockStatus.StatusReady, AsyncLockStatus.StatusReleasing);
			// Release the local lock.
			AsyncLock?.ReleaseLock();
		}

		public static async Task WaitForAllMutexToCloseAsync()
		{
			DateTime start = DateTime.Now;
			lock (AsyncMutexesLock)
			{
				foreach (var mutex in AsyncMutexes)
				{
					mutex.Value.IsQuitPending = true;
				}
			}

			while (true)
			{
				lock (AsyncMutexesLock)
				{
					bool stillRunning = AsyncMutexes.Any(am => am.Value.IsAlive);
					if (!stillRunning)
					{
						return;
					}
					Logger.LogDebug($"Waiting for: {string.Join(", ", AsyncMutexes.Where(am => am.Value.IsAlive).Select(m => m.Value.ShortName))}.");
				}
				await Task.Delay(200);
				if (DateTime.Now - start > TimeSpan.FromSeconds(60))
				{
					var mutexesAlive = AsyncMutexes.Where(am => am.Value.IsAlive).Select(m => m.Value.ShortName);
					var names = string.Join(", ", mutexesAlive);
					throw new TimeoutException($"{nameof(AsyncMutex)}(es) still alive after Timeout: {names}.");
				}
			}
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
							Logger.LogWarning(ex);
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
