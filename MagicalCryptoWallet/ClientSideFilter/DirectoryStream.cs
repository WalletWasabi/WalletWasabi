using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MagicalCryptoWallet.Backend
{
	internal class DirectoryStream : MultiStream
	{
		private readonly DirectoryInfo _directoryInfo;
		private readonly string _pattern;

		public static DirectoryStream Open(DirectoryInfo directoryInfo, string pattern)
		{
			var streams = GetStreams(directoryInfo, pattern);
			return new DirectoryStream(directoryInfo, streams, pattern);
		} 

		private DirectoryStream(DirectoryInfo directoryInfo, IEnumerable<Stream> streams, string pattern)
		{
			_directoryInfo = directoryInfo;
			_pattern = pattern;
			SetStreams(streams);
		}

		private static IEnumerable<Stream> GetStreams(DirectoryInfo directoryInfo, string pattern)
		{
			EnsureDirectoryExists(directoryInfo);
			var orderedFiles = directoryInfo.EnumerateFiles(pattern).OrderBy(x => x.Name);
			foreach (var file in orderedFiles)
			{
				yield return file.Open(FileMode.Open);
			}
		}

		private static void EnsureDirectoryExists(DirectoryInfo directoryInfo)
		{
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
		}

		protected override Stream CreateNewStream()
		{
			var orderedFiles = _directoryInfo.EnumerateFiles(_pattern).OrderBy(x => x.Name);
			var lastFileName = orderedFiles.LastOrDefault()?.Name ?? _pattern.Replace("????", "0000");
			var iod = lastFileName.IndexOf("-");
			var numericPart = lastFileName.Substring(iod+1, 4);
			var nextNumber = int.Parse(numericPart) + 1;
			var nextFileName = lastFileName.Replace(numericPart, $"{nextNumber:0000}");
			return new FileStream(Path.Combine(_directoryInfo.FullName, nextFileName), FileMode.CreateNew);
		}
	}

	internal abstract class MultiStream : Stream
	{
		private const long MaxStreamSize = (2 * 1024 * 1024);

		private List<Stream> _streams;

		private volatile Stream _currentStream;
		private volatile int _currentStreamIndex;
		private long _length = -1;
		private long _position;

		protected void SetStreams(IEnumerable<Stream> streams)
		{
			var enumerable = streams as Stream[] ?? streams.ToArray();
			_streams = enumerable.ToList();
			if (_streams.Count == 0)
				AddNewStream();
			_currentStreamIndex = 0;
			_currentStream = _streams.FirstOrDefault();
		}

		private void AddNewStream()
		{
			_streams.Add(CreateNewStream());
		}

		private IEnumerable<long> StreamsLenght
		{
			get
			{
				foreach (var stream in _streams)
				{
					yield return stream.Length;
				}
			}
		}

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanWrite => Length == _position;

		public override void Flush()
		{
			_currentStream?.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			var pos = 0L;
			var streamIndex = 0;
			var streamsLength = StreamsLenght.ToArray();

			if (origin == SeekOrigin.Begin)
			{
				if (offset > Length)
					throw new ArgumentOutOfRangeException(nameof(offset));

				while (pos + streamsLength[streamIndex] < offset)
				{
					pos += streamsLength[streamIndex];
					streamIndex++;
				}

				if (streamIndex != _currentStreamIndex)
				{
					_currentStreamIndex = streamIndex;
					_currentStream = _streams[_currentStreamIndex];
				}

				_currentStream.Seek(offset - pos, origin);

				_position = offset;
			}
			else
			{
				var finalPosition = _position + offset;

				if (finalPosition >= Length || finalPosition < 0)
					throw new ArgumentOutOfRangeException(nameof(offset));

				while (pos + streamsLength[streamIndex] <= offset + _position)
				{
					pos += streamsLength[streamIndex];
					streamIndex++;
				}

				if (streamIndex != _currentStreamIndex)
				{
					_currentStreamIndex = streamIndex;
					_currentStream = _streams[_currentStreamIndex];
				}
				_currentStream.Seek(offset, origin);

				_position += offset;
			}
			return offset;
		}

		public override void SetLength(long value)
		{
			throw new InvalidOperationException("It is not possible to set the Stream length.");
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var result = 0;
			var buffPostion = offset;

			while (count > 0)
			{
				var bytesRead = _currentStream.Read(buffer, buffPostion, count);
				result += bytesRead;
				buffPostion += bytesRead;
				_position += bytesRead;
				count -= bytesRead;

				if (count > 0 && _currentStream.Position == _currentStream.Length)
				{
					_currentStreamIndex++;
					_currentStream = _streams[_currentStreamIndex];
					_currentStream.Seek(0, SeekOrigin.Begin);
				}
			}

			return result;
		}

		public override long Length
		{
			get
			{
				var length = 0L;
				foreach (var stream in _streams)
				{
					length += stream.Length;
				}

				return length;
			}
		}

		public override long Position
		{
			get => _position;
			set => Seek(value, SeekOrigin.Begin);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var rest = (int)(_currentStream.Position + count - MaxStreamSize);
			if (rest <= 0)
			{
				_currentStream.Write(buffer, offset, count);
			}
			else
			{
				_currentStream.Write(buffer, offset, count - rest);
				_currentStream.Flush();
				AddNewStream();
				_currentStreamIndex++;
				_currentStream = _streams[_currentStreamIndex];
				_currentStream.Write(buffer, offset + count - rest, rest);
			}

			_position += count;
		}

		protected override void Dispose(bool disposing)
		{
			foreach (var stream in _streams)
			{
				stream.Dispose();
			}
		}

		protected abstract Stream CreateNewStream();
	}
}
