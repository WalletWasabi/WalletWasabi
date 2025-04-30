using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WalletWasabi.Services;

// A typed message queue for asynchronous message processing.
public class Mailbox<TMsg>
{
	private readonly Channel<TMsg> _channel = Channel.CreateUnbounded<TMsg>(
		new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false,
			AllowSynchronousContinuations = false
		});

	// Posts a message to the mailbox.
	internal bool Post(TMsg msg) =>
		_channel.Writer.TryWrite(msg);

	// Asynchronously receives the next message from the mailbox.
	internal ValueTask<TMsg> ReceiveAsync(CancellationToken cancellationToken) =>
		_channel.Reader.ReadAsync(cancellationToken);

	// Completes the mailbox, preventing further messages from being posted.
	internal void Complete() =>
		_channel.Writer.Complete();
}

public sealed class MailboxProcessor<TMsg>(
	Func<Mailbox<TMsg>, CancellationToken, Task> body,
	CancellationToken? cancellationToken = null) : IDisposable
{
	private readonly Mailbox<TMsg> _mailbox = new();

	private readonly Func<Mailbox<TMsg>, CancellationToken, Task> _body =
		body ?? throw new ArgumentNullException(nameof(body));

	private readonly CancellationTokenSource _cts = cancellationToken != null
		? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value)
		: new CancellationTokenSource();

	private Task? _processingTask;
	private bool _isDisposed;

	public void Start()
	{
		if (_processingTask != null)
		{
			throw new InvalidOperationException("The processor has already been started.");
		}

		_processingTask = Task.Run(InternalStartAsync, _cts.Token);
	}

	public bool Post(TMsg message)
	{
		return !_isDisposed && _mailbox.Post(message);
	}

	public Task<TReply> PostAndReplyAsync<TReply>(Func<IReplyChannel<TReply>, TMsg> messageFactory,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		var tcs = new TaskCompletionSource<TReply>();
		var replyChannel = new ReplyChannel<TReply>(reply =>
		{
			try
			{
				tcs.TrySetResult(reply);
			}
			catch (Exception ex) // it only throws ObjectDisposedException
			{
				tcs.TrySetException(ex);
			}
		});

		var message = messageFactory(replyChannel);
		if (!Post(message))
		{
			tcs.TrySetException(new InvalidProgramException(
				"It was not possible to write into an Unbounded channel, something that should always succeed."));
		}

		return tcs.Task.WaitAsync(cancellationToken);
	}

	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		_isDisposed = true;
		_mailbox.Complete();
		_cts.Cancel();
		_cts.Dispose();
	}

	private async Task InternalStartAsync()
	{
		try
		{
			await _body(_mailbox, _cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
		{
			// Normal cancellation, ignore
		}
		catch (Exception exception)
		{
			// Log the exception
			Workers.Tell("logger", exception);
		}
	}
}

public interface IReplyChannel<in TReply>
{
	void Reply(TReply reply);
}

internal sealed class ReplyChannel<TReply> : IReplyChannel<TReply>
{
	private readonly Action<TReply> _replyAction;

	internal ReplyChannel(Action<TReply> replyAction)
	{
		_replyAction = replyAction ?? throw new ArgumentNullException(nameof(replyAction));
	}

	public void Reply(TReply reply) => _replyAction(reply);
}

public static class Workers
{
	private static readonly ConcurrentDictionary<string, object> Processors = new();

	public static MailboxProcessor<TMsg> Spawn<TMsg>(string name, Func<Mailbox<TMsg>, CancellationToken, Task> body)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
		ArgumentNullException.ThrowIfNull(body, nameof(body));

		if (Processors.ContainsKey(name))
		{
			throw new ArgumentException($"A worker named '{name}' already exists.", nameof(name));
		}

		var processor = new MailboxProcessor<TMsg>(body);
		processor.Start();
		Processors[name] = processor;
		return processor;
	}

	public static bool Tell<TMsg>(string name, TMsg msg)
	{
		if (Processors.TryGetValue(name, out var p) && p is MailboxProcessor<TMsg> typedProcessor)
		{
			return typedProcessor.Post(msg);
		}

		return false;
	}

	public static Func<Mailbox<TMsg>, CancellationToken, Task> Periodically<TMsg>(TimeSpan period,
		Func<TMsg, CancellationToken, Task> handler) =>
		async (inbox, cancellationToken) =>
		{
			var lastUpdateTime = DateTime.MinValue;
			while (!cancellationToken.IsCancellationRequested)
			{
				var msg = await inbox.ReceiveAsync(cancellationToken).ConfigureAwait(false);
				if (DateTime.UtcNow - lastUpdateTime > period)
				{
					await handler(msg, cancellationToken).ConfigureAwait(false);
					lastUpdateTime = DateTime.UtcNow;
				}
			}
		};

	public static Func<Mailbox<TMsg>, CancellationToken, Task> Periodically<TMsg, TState>(TimeSpan period, TState state,
		Func<TMsg, TState, CancellationToken, Task<TState>> handler) =>
		async (inbox, cancellationToken) =>
		{
			var lastUpdateTime = DateTime.MinValue;
			while (!cancellationToken.IsCancellationRequested)
			{
				var msg = await inbox.ReceiveAsync(cancellationToken).ConfigureAwait(false);
				if (DateTime.UtcNow - lastUpdateTime > period)
				{
					state = await handler(msg, state, cancellationToken).ConfigureAwait(false);
					lastUpdateTime = DateTime.UtcNow;
				}
			}
		};
}

public static class EventBusExtensions
{
	public static IDisposable Subscribe<TMsg>(this EventBus eventBus, MailboxProcessor<TMsg> processor)
		where TMsg : notnull
	{
		ArgumentNullException.ThrowIfNull(eventBus, nameof(eventBus));
		ArgumentNullException.ThrowIfNull(processor, nameof(processor));

		return eventBus.Subscribe<TMsg>(msg => processor.Post(msg));
	}
}
