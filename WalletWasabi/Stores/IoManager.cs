using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// Safely manager file operations.
	/// </summary>
	public class IoManager
	{
		public string FilePath { get; }
		public string FileName { get; }
		public string FileNameWithoutExtension { get; }
		private string SemaphoreName { get; }

		public IoManager(string filePath)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
			FileName = Path.GetFileName(FilePath);
			FileNameWithoutExtension = Path.GetFileNameWithoutExtension(FilePath);

			// https://docs.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=netframework-4.8
			// On a server that is running Terminal Services, a named system mutex can have two levels of visibility.
			// If its name begins with the prefix "Global\", the mutex is visible in all terminal server sessions.
			// If its name begins with the prefix "Local\", the mutex is visible only in the terminal server session where it was created.
			// In that case, a separate mutex with the same name can exist in each of the other terminal server sessions on the server.
			// If you do not specify a prefix when you create a named mutex, it takes the prefix "Local\".
			// Within a terminal server session, two mutexes whose names differ only by their prefixes are separate mutexes,
			// and both are visible to all processes in the terminal server session.
			// That is, the prefix names "Global\" and "Local\" describe the scope of the mutex name relative to terminal server sessions, not relative to processes.
			SemaphoreName = $"Global\\4AA0E5A2-A94F-4B92-B962-F2BBC7A68323-{FileNameWithoutExtension}";
		}

		public async Task WrapInSemaphoreAsync(Func<Task> todo)
		{
			using (var semaphore = new Semaphore(1, 1, SemaphoreName))
			{
				await AcquireSemaphoreAsync(semaphore);

				try
				{
					await todo();
				}
				finally
				{
					semaphore.Release();
				}
			}
		}

		private async Task AcquireSemaphoreAsync(Semaphore semaphore)
		{
			bool semaphoreAcquired = false;
			try
			{
				// acquire the mutex (or timeout after 60 seconds)
				// will return false if it timed out
				var start = DateTime.Now;
				while (DateTime.Now - start < TimeSpan.FromSeconds(90))
				{
					semaphoreAcquired = semaphore.WaitOne(1);

					if (semaphoreAcquired)
					{
						break;
					}
					await Task.Delay(1000);
				}
			}
			catch (AbandonedMutexException)
			{
				// abandoned mutexes are still acquired, we just need
				// to handle the exception and treat it as acquisition
				semaphoreAcquired = true;
			}

			if (semaphoreAcquired == false)
			{
				throw new IOException("Couldn't acquire Semaphore on the file.");
			}
		}

		public async Task WrapInMutexAsync(Action todo)
		{
			using (var semaphore = new Semaphore(1, 1, SemaphoreName))
			{
				await AcquireSemaphoreAsync(semaphore);

				try
				{
					todo();
				}
				finally
				{
					semaphore.Release();
				}
			}
		}
	}
}
