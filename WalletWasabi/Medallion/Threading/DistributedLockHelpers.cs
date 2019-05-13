using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Threading
{
	internal static class DistributedLockHelpers
	{
		public static int ToInt32Timeout(this TimeSpan timeout, string paramName = null)
		{
			// based on http://referencesource.microsoft.com/#mscorlib/system/threading/Tasks/Task.cs,959427ac16fa52fa

			var totalMilliseconds = (long)timeout.TotalMilliseconds;
			if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
			{
				throw new ArgumentOutOfRangeException(paramName ?? "timeout");
			}

			return (int)totalMilliseconds;
		}

		public static Task<IDisposable> AcquireAsync(IDistributedLock @lock, TimeSpan? timeout, CancellationToken cancellationToken)
		{
			var tryAcquireTask = @lock.TryAcquireAsync(timeout ?? Timeout.InfiniteTimeSpan, cancellationToken);
			return ValidateTryAcquireResultAsync(tryAcquireTask, timeout);
		}

		public static IDisposable Acquire(IDistributedLock @lock, TimeSpan? timeout, CancellationToken cancellationToken)
		{
			var handle = @lock.TryAcquire(timeout ?? Timeout.InfiniteTimeSpan, cancellationToken);
			ValidateTryAcquireResult(handle, timeout);

			return handle;
		}

		public static async Task<THandle> ValidateTryAcquireResultAsync<THandle>(Task<THandle> tryAcquireTask, TimeSpan? timeout)
			where THandle : class, IDisposable
		{
			var handle = await tryAcquireTask;
			ValidateTryAcquireResult(handle, timeout);

			return handle;
		}

		public static void ValidateTryAcquireResult(IDisposable handle, TimeSpan? timeout) =>
			ValidateTryAcquireResult(succeeded: handle != null, timeout: timeout);

		public static void ValidateTryAcquireResult(bool succeeded, TimeSpan? timeout)
		{
			if (!succeeded)
			{
				if (timeout.HasValue && timeout >= TimeSpan.Zero)
					throw new TimeoutException("Timeout exceeded when trying to acquire the lock");

				// should never get here
				throw new InvalidOperationException("Failed to acquire the lock");
			}
		}

		public static IDisposable TryAcquireWithAsyncCancellation(IDistributedLock @lock, TimeSpan timeout, CancellationToken cancellationToken)
		{
			var tryAcquireTask = @lock.TryAcquireAsync(timeout, cancellationToken);
			try
			{
				return tryAcquireTask.Result;
			}
			catch (AggregateException ex)
			{
				// attempt to prevent the throwing of aggregate exceptions
				if (ex.InnerExceptions.Count == 1)
				{
					// rethrow the inner exception without losing stack trace
					ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				}
				throw; // otherwise just rethrow
			}
		}

		public static string ToSafeLockName(string baseLockName, int maxNameLength, Func<string, string> convertToValidName)
		{
			if (baseLockName == null)
				throw new ArgumentNullException("baseLockName");

			var validBaseLockName = convertToValidName(baseLockName);
			if (validBaseLockName == baseLockName && validBaseLockName.Length <= maxNameLength)
			{
				return baseLockName;
			}

			using (var sha = SHA512.Create())
			{
				var hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(baseLockName)));

				if (hash.Length >= maxNameLength)
				{
					return hash.Substring(0, length: maxNameLength);
				}

				var prefix = validBaseLockName.Substring(0, Math.Min(validBaseLockName.Length, maxNameLength - hash.Length));
				return prefix + hash;
			}
		}
	}
}
