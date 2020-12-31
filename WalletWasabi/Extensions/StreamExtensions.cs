using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
	public static class StreamExtensions
	{
		public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken ctsToken = default)
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Shared;
			byte[] buffer = pool.Rent(1);
			try
			{
				int len = await stream.ReadAsync(buffer.AsMemory(0, 1), ctsToken).ConfigureAwait(false);

				// End of stream.
				if (len == 0)
				{
					return -1;
				}

				return buffer[0];
			}
			finally
			{
				pool.Return(buffer);
			}
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

		/// <summary>
		/// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/>.
		/// </summary>
		/// <param name="stream">Stream to read from.</param>
		/// <param name="buffer">Buffer whose length must be at least <paramref name="count"/> elements.</param>
		/// <param name="count">Number of bytes to read.</param>
		/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
		/// <returns><c>true</c> if we could read exactly <paramref name="count"/> bytes from stream (stream may contain more bytes though).</returns>
		public static async Task<bool> ReadExactlyAsync(this Stream stream, byte[] buffer, int count, CancellationToken cancellationToken = default)
		{
			int offset = 0;
			while (offset < count)
			{
				int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
				if (read == 0)
				{
					return false;
				}
				offset += read;
			}

			return true;
		}
	}
}
