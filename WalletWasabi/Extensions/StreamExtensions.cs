using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Extensions;

public static class StreamExtensions
{
	/// <summary>
	/// Reads a single byte from <paramref name="stream"/>.
	/// </summary>
	/// <param name="stream">Stream to read from.</param>
	/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
	/// <returns><c>-1</c> when no byte could be read, otherwise valid byte value (cast <see cref="int"/> result to <see cref="byte"/>).</returns>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken cancellationToken)
	{
		ArrayPool<byte> pool = ArrayPool<byte>.Shared;
		byte[] buffer = pool.Rent(1);
		try
		{
			int len = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);

			// We prefer to throw the exception rather than returning -1 as the cancellation token parameter of Stream.ReadAsync() method
			// is advisory: https://github.com/dotnet/runtime/issues/24093#issuecomment-544941928.
			cancellationToken.ThrowIfCancellationRequested();

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
	/// <returns>Number of read bytes. At most <paramref name="count"/>.</returns>
	/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
	public static async Task<int> ReadBlockAsync(this Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
	{
		int remaining = count;
		while (remaining != 0)
		{
			int read = await stream.ReadAsync(buffer.AsMemory(count - remaining, remaining), cancellationToken).ConfigureAwait(false);

			// We prefer to throw the exception rather than breaking here as the cancellation token parameter of Stream.ReadAsync() method
			// is advisory: https://github.com/dotnet/runtime/issues/24093#issuecomment-544941928.
			cancellationToken.ThrowIfCancellationRequested();

			// End of stream.
			if (read == 0)
			{
				break;
			}

			remaining -= read;
		}
		return count - remaining;
	}
}
