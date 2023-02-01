using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

public class AsyncQueue<T>
{
	private Channel<T> _channel;

	public AsyncQueue()
	{
		UnboundedChannelOptions unboundedChannelOptions = new()
		{
			SingleReader = false,
			SingleWriter = false
		};
		_channel = Channel.CreateUnbounded<T>(unboundedChannelOptions);
	}

	public async Task<T> DequeueAsync(CancellationToken cancellationToken)
	{
		return await _channel.Reader.ReadAsync(cancellationToken);
	}

	public IAsyncEnumerable<T> GetAsyncIterator(CancellationToken cancellationToken)
	{
		return _channel.Reader.ReadAllAsync(cancellationToken);
	}

	public void Enqueue(T item)
	{
		if (!_channel.Writer.TryWrite(item))
		{
			throw new InvalidOperationException($"Cannot write to channel.");
		}
	}
}
