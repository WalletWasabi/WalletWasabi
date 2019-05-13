using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Threading
{
	/// <summary>
	/// Implements a system-/OS-scoped distributed lock using .NET <see cref="EventWaitHandle"/>s
	/// </summary>
	public sealed class SystemDistributedLock : IDistributedLock
	{
		private const string GlobalPrefix = @"Global\";
		private static readonly TimeSpan DefaultAbandonmentCheckFrequency = TimeSpan.FromSeconds(2);

		private readonly string LockName;
		private readonly TimeSpan AbandonmentCheckFrequency;

		/// <summary>
		/// Creates an instance of <see cref="SystemDistributedLock"/> named <paramref name="lockName"/>.
		/// <paramref name="abandonmentCheckFrequency"/> specifies how long the lock should wait before checking
		/// if the underlying <see cref="EventWaitHandle"/> has been abandoned (defaults to 2 seconds)
		/// </summary>
		public SystemDistributedLock(string lockName, TimeSpan? abandonmentCheckFrequency = default)
		{
			// note that just Global\ is not a valid name
			if (string.IsNullOrEmpty(lockName))
				throw new ArgumentNullException("lockName is required");
			if (lockName.Length > MaxLockNameLength)
				throw new FormatException("lockName: must be at most " + MaxLockNameLength + " characters");
			// from http://stackoverflow.com/questions/18392175/net-system-wide-eventwaithandle-name-allowed-characters
			if (lockName.IndexOf('\\') >= 0)
				throw new FormatException(@"lockName: must not contain '\'");

			if (abandonmentCheckFrequency.HasValue)
			{
				// must be a valid timeout
				var abandonmentCheckFrequencyMillis = abandonmentCheckFrequency.Value.ToInt32Timeout("abandonmentCheckFrequency");
				if (abandonmentCheckFrequencyMillis == 0)
					throw new ArgumentOutOfRangeException("abandonmentCheckFrequency: must be non-zero");
				AbandonmentCheckFrequency = abandonmentCheckFrequency.Value;
			}
			else
			{
				AbandonmentCheckFrequency = DefaultAbandonmentCheckFrequency;
			}

			LockName = GlobalPrefix + lockName;
		}

		#region ---- Public API ----

		/// <summary>
		/// Attempts to acquire the lock synchronously. Usage:
		/// <code>
		///     using (var handle = myLock.TryAcquire(...))
		///     {
		///         if (handle != null) { /* we have the lock! */ }
		///     }
		///     // dispose releases the lock if we took it
		/// </code>
		/// </summary>
		/// <param name="timeout">How long to wait before giving up on acquiring the lock. Defaults to 0</param>
		/// <param name="cancellationToken">Specifies a token by which the wait can be canceled</param>
		/// <returns>An <see cref="IDisposable"/> "handle" which can be used to release the lock, or null if the lock was not taken</returns>
		public IDisposable TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default)
		{
			var timeoutMillis = timeout.ToInt32Timeout();
			var abandonmentCheckFrequencyMillis = AbandonmentCheckFrequency.ToInt32Timeout();

			var @event = CreateEvent();
			var cleanup = true;
			try
			{
				if (abandonmentCheckFrequencyMillis <= 0)
				{
					// no abandonment check: just acquire once
					if (TryAcquireOnce(@event, timeoutMillis, cancellationToken))
					{
						cleanup = false;
						return new EventScope(@event);
					}
					return null;
				}

				if (timeoutMillis < 0)
				{
					// infinite timeout: just loop forever with the abandonment check
					while (true)
					{
						if (TryAcquireOnce(@event, abandonmentCheckFrequencyMillis, cancellationToken))
						{
							cleanup = false;
							return new EventScope(@event);
						}

						// refresh the event in case it was abandoned by the original owner
						@event.Dispose();
						@event = CreateEvent();
					}
				}

				// fixed timeout: loop in abandonment check chunks
				var elapsedMillis = 0;
				do
				{
					var nextWaitMillis = Math.Min(abandonmentCheckFrequencyMillis, timeoutMillis - elapsedMillis);
					if (TryAcquireOnce(@event, nextWaitMillis, cancellationToken))
					{
						cleanup = false;
						return new EventScope(@event);
					}

					elapsedMillis += nextWaitMillis;

					// refresh the event in case it was abandoned by the original owner
					@event.Dispose();
					@event = CreateEvent();
				}
				while (elapsedMillis < timeoutMillis);

				return null;
			}
			catch
			{
				// just in case we fail to create a scope or something
				throw;
			}
			finally
			{
				if (cleanup)
				{
					@event.Dispose();
				}
			}
		}

		/// <summary>
		/// Acquires the lock synchronously, failing with <see cref="TimeoutException"/> if the wait times out
		/// <code>
		///     using (myLock.Acquire(...))
		///     {
		///         // we have the lock
		///     }
		///     // dispose releases the lock
		/// </code>
		/// </summary>
		/// <param name="timeout">How long to wait before giving up on acquiring the lock. Defaults to <see cref="Timeout.InfiniteTimeSpan"/></param>
		/// <param name="cancellationToken">Specifies a token by which the wait can be canceled</param>
		/// <returns>An <see cref="IDisposable"/> "handle" which can be used to release the lock</returns>
		public IDisposable Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return DistributedLockHelpers.Acquire(this, timeout, cancellationToken);
		}

		/// <summary>
		/// Attempts to acquire the lock asynchronously. Usage:
		/// <code>
		///     using (var handle = await myLock.TryAcquireAsync(...))
		///     {
		///         if (handle != null) { /* we have the lock! */ }
		///     }
		///     // dispose releases the lock if we took it
		/// </code>
		/// </summary>
		/// <param name="timeout">How long to wait before giving up on acquiring the lock. Defaults to 0</param>
		/// <param name="cancellationToken">Specifies a token by which the wait can be canceled</param>
		/// <returns>An <see cref="IDisposable"/> "handle" which can be used to release the lock, or null if the lock was not taken</returns>
		public Task<IDisposable> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
		{
			var timeoutMillis = timeout.ToInt32Timeout();

			return InternalTryAcquireAsync(timeoutMillis, cancellationToken);
		}

		/// <summary>
		/// Acquires the lock asynchronously, failing with <see cref="TimeoutException"/> if the wait times out
		/// <code>
		///     using (await myLock.AcquireAsync(...))
		///     {
		///         // we have the lock
		///     }
		///     // dispose releases the lock
		/// </code>
		/// </summary>
		/// <param name="timeout">How long to wait before giving up on acquiring the lock. Defaults to <see cref="Timeout.InfiniteTimeSpan"/></param>
		/// <param name="cancellationToken">Specifies a token by which the wait can be canceled</param>
		/// <returns>An <see cref="IDisposable"/> "handle" which can be used to release the lock</returns>
		public Task<IDisposable> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
		{
			return DistributedLockHelpers.AcquireAsync(this, timeout, cancellationToken);
		}

		/// <summary>
		/// The maximum allowed length for lock names
		/// </summary>
		// 260 based on LINQPad experimentation
		public static int MaxLockNameLength => 260 - GlobalPrefix.Length;

		/// <summary>
		/// Given <paramref name="baseLockName"/>, constructs a lock name which is safe for use with <see cref="SystemDistributedLock"/>
		/// </summary>
		public static string GetSafeLockName(string baseLockName)
		{
			return DistributedLockHelpers.ToSafeLockName(baseLockName, MaxLockNameLength, s => s.Length == 0 ? "EMPTY" : s.Replace('\\', '_'));
		}

		#endregion ---- Public API ----

		private async Task<IDisposable> InternalTryAcquireAsync(int timeoutMillis, CancellationToken cancellationToken)
		{
			var abandonmentCheckFrequencyMillis = AbandonmentCheckFrequency.ToInt32Timeout();

			var @event = CreateEvent();
			var cleanup = true;
			try
			{
				if (abandonmentCheckFrequencyMillis <= 0)
				{
					// no abandonment check: just acquire once
					if (await @event.WaitOneAsync(TimeSpan.FromMilliseconds(timeoutMillis), cancellationToken))
					{
						cleanup = false;
						return new EventScope(@event);
					}
					return null;
				}

				if (timeoutMillis < 0)
				{
					// infinite timeout: just loop forever with the abandonment check
					while (true)
					{
						if (await @event.WaitOneAsync(AbandonmentCheckFrequency, cancellationToken))
						{
							cleanup = false;
							return new EventScope(@event);
						}

						// refresh the event in case it was abandoned by the original owner
						@event.Dispose();
						@event = CreateEvent();
					}
				}

				// fixed timeout: loop in abandonment check chunks
				var elapsedMillis = 0;
				do
				{
					var nextWaitMillis = Math.Min(abandonmentCheckFrequencyMillis, timeoutMillis - elapsedMillis);
					if (await @event.WaitOneAsync(TimeSpan.FromMilliseconds(nextWaitMillis), cancellationToken))
					{
						cleanup = false;
						return new EventScope(@event);
					}

					elapsedMillis += nextWaitMillis;

					// refresh the event in case it was abandoned by the original owner
					@event.Dispose();
					@event = CreateEvent();
				}
				while (elapsedMillis < timeoutMillis);

				return null;
			}
			catch
			{
				// just in case we fail to create a scope or something
				throw;
			}
			finally
			{
				if (cleanup)
				{
					@event.Dispose();
				}
			}
		}

		private static bool TryAcquireOnce(EventWaitHandle @event, int timeoutMillis, CancellationToken cancellationToken)
		{
			// cancellation case
			if (cancellationToken.CanBeCanceled)
			{
				// ensures that if we are already canceled upon entering this method
				// we will cancel, not wait
				cancellationToken.ThrowIfCancellationRequested();

				// cancellable wait based on
				// http://www.thomaslevesque.com/2015/06/04/async-and-cancellation-support-for-wait-handles/
				var index = WaitHandle.WaitAny(new[] { @event, cancellationToken.WaitHandle }, timeoutMillis);
				switch (index)
				{
					case WaitHandle.WaitTimeout: // timeout
						@event.Dispose();
						return false;

					case 0: // event
						return true;

					default: // canceled
						cancellationToken.ThrowIfCancellationRequested();
						throw new InvalidOperationException("Should never get here");
				}
			}

			// normal case
			return @event.WaitOne(timeoutMillis);
		}

		private EventWaitHandle CreateEvent()
		{
			// based on http://stackoverflow.com/questions/2590334/creating-a-cross-process-eventwaithandle
			//var security = new EventWaitHandleSecurity();
			//// allow anyone to wait on and signal this lock
			//security.AddAccessRule(
			//	new EventWaitHandleAccessRule(
			//		new SecurityIdentifier(WellKnownSidType.WorldSid, domainSid: null),
			//		EventWaitHandleRights.FullControl, // doesn't seem to work without this :-/
			//		AccessControlType.Allow
			//	)
			//);
			var @event = new EventWaitHandle(
				// if we create, start as unlocked
				initialState: true,
				// allow only one thread to hold the lock
				mode: EventResetMode.AutoReset,
				name: LockName,
				createdNew: out _
			);
			//@event.SetAccessControl(security);

			return @event;
		}

		private sealed class EventScope : IDisposable
		{
			private EventWaitHandle _event;

			public EventScope(EventWaitHandle @event)
			{
				_event = @event;
			}

			void IDisposable.Dispose()
			{
				var @event = Interlocked.Exchange(ref _event, null);
				if (@event != null)
				{
					@event.Set(); // signal
					@event.Dispose();
				}
			}
		}
	}
}
