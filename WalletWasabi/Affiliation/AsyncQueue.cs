using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

public class AsyncQueue<T>
{
	private Channel<T> _channel { get; }

	public AsyncQueue()
	{
		_channel = Channel.CreateUnbounded<T>(
			new UnboundedChannelOptions
			{
				SingleReader = false,
				SingleWriter = false
			}
		);
	}

	public async Task<T> DequeueAsync(CancellationToken cancellationToken)
	{
		return await _channel.Reader.ReadAsync(cancellationToken);
	}

	public IAsyncEnumerable<T> GetAsyncIterator(CancellationToken cancellationToken)
	{
		return _channel.Reader.ReadAllAsync();
	}

	public void Enqueue(T item)
	{
		if (!_channel.Writer.TryWrite(item))
		{
			throw new InvalidOperationException($"Cannot write to channel.");
		}
	}
}
