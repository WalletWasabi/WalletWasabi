using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Stores
{
	public class IoAsyncMutexProvider
	{
		public string FilePath { get; }

		public string FileName { get; }
		public string FileNameWithoutExtension { get; }
		public AsyncMutex Mutex { get; }

		public IoAsyncMutexProvider(string filePath)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);

			FileName = Path.GetFileName(FilePath);
			var shortHash = HashHelpers.GenerateSha256Hash(FilePath).Substring(0, 7);
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
			Mutex = new AsyncMutex($"{FileNameWithoutExtension}-{shortHash}");
		}
	}
}
