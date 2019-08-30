using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Io
{
	public class IoManager
	{
		public string FilePath { get; }

		public string FileName { get; }
		public string FileNameWithoutExtension { get; }

		public IoManager(string filePath)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
			FileName = Path.GetFileName(FilePath);
			FileNameWithoutExtension = Path.GetFileNameWithoutExtension(FilePath);
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
			if (File.Exists(sourcePath))
			{
				File.Move(sourcePath, FilePath);

				return true;
			}
			else
			{
				return false;
			}
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

		public async Task WriteAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
		{
			if (lines is null || !lines.Any())
			{
				return;
			}

			IoHelpers.EnsureContainingDirectoryExists(FilePath);

			await File.WriteAllLinesAsync(FilePath, lines, cancellationToken);
		}

		public async Task AppendAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
		{
			if (lines is null || !lines.Any())
			{
				return;
			}

			await File.AppendAllLinesAsync(FilePath, lines, cancellationToken);
		}

		#endregion IoOperations
	}
}
