using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace WalletWasabi.Affiliation;

public class AsyncQueue<T>
{
	private readonly Channel<T> _channel;

	public AsyncQueue()
	{
		UnboundedChannelOptions options = new()
		{
			SingleReader = false,
			SingleWriter = false
		};

		_channel = Channel.CreateUnbounded<T>(options);
	}

	public IAsyncEnumerable<T> GetAsyncIterator(CancellationToken cancellationToken)
	{
		return _channel.Reader.ReadAllAsync(cancellationToken);
	}

	public void Enqueue(T item)
	{
		if (!_channel.Writer.TryWrite(item))
		{
			throw new InvalidOperationException("Cannot write to channel.");
		}
	}
}
