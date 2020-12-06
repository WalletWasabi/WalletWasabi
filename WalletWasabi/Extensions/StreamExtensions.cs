using System.Threading;
using System.Threading.Tasks;
using System;

namespace System.IO
{
	public static class StreamExtensions
	{
		public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken ctsToken = default)
		{
			var buf = new byte[1];
			int len = await stream.ReadAsync(buf.AsMemory(0, 1), ctsToken).ConfigureAwait(false);
			if (len == 0)
			{
				return -1;
			}

			return buf[0];
		}

		public static async Task<int> ReadBlockAsync(this Stream stream, byte[] buffer, int count, CancellationToken ctsToken = default)
		{
			int left = count;
			while (left != 0)
			{
				int read = await stream.ReadAsync(buffer.AsMemory(count - left, left), ctsToken).ConfigureAwait(false);
				left -= read;
			}
			return count - left;
		}
	}
}
