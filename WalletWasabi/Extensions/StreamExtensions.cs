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

		/// <summary>
		/// Attempts to read <paramref name="count"/> bytes from <paramref name="stream"/>.
		/// </summary>
		/// <param name="stream">Stream to read from.</param>
		/// <param name="buffer">Buffer whose length must be at least <paramref name="count"/> elements.</param>
		/// <param name="count">Number of bytes to read.</param>
		/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
		/// <returns><c>0</c> when all <paramref name="count"/> bytes were read from the stream. Otherwise, a number of remaining bytes that were not transmitted.</returns>
		public static async Task<int> ReadBlockAsync(this Stream stream, byte[] buffer, int count, CancellationToken cancellationToken = default)
		{
			int left = count;
			while (left != 0)
			{
				int read = await stream.ReadAsync(buffer.AsMemory(count - left, left), cancellationToken).ConfigureAwait(false);

				// End of stream.
				if (read == 0)
				{
					break;
				}

				left -= read;
			}
			return count - left;
		}
	}
}
