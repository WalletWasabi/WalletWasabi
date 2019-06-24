using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Stores
{
	public class IoManager : IoAsyncMutexProvider
	{
		public IoManager(string filePath) : base(filePath)
		{
		}

		#region IoOperations

		public void DeleteMe()
		{
			if (File.Exists(FilePath))
			{
				File.Delete(FilePath);
			}
		}

		public bool TryReplaceMeWith(string sourcePath)
		{
			if (!File.Exists(sourcePath))
			{
				return false;
			}

			File.Move(sourcePath, FilePath);

			return true;
		}

		public bool Exists()
		{
			return File.Exists(FilePath);
		}

		public async Task<string[]> ReadAllLinesAsync(CancellationToken cancellationToken = default)
		{
			return await ReadAllLinesAsync(FilePath, cancellationToken);
		}

		protected static async Task<string[]> ReadAllLinesAsync(string filePath, CancellationToken cancellationToken)
		{
			return await File.ReadAllLinesAsync(filePath, cancellationToken);
		}

		/// <summary>
		/// Open text file and read a specified amount of data. This method is useful when you want async/await read/write
		/// but in a performant way.
		/// </summary>
		/// <returns>The StreamReader where you can use ReadLineAsync() for example.</returns>
		/// <param name="bufferSize">Size of the bytes to handle sync way. The default is 1Mb.</param>
		public StreamReader OpenText(int bufferSize = Constants.BigFileReadWriteBufferSize)
		{
			return OpenText(FilePath, bufferSize);
		}

		protected static StreamReader OpenText(string filePath, int bufferSize = Constants.BigFileReadWriteBufferSize)
		{
			var fs = File.OpenRead(filePath);
			return new StreamReader(fs, Encoding.ASCII, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize, leaveOpen: false);
		}

		#endregion IoOperations
	}
}
