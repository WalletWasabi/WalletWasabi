using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

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

public delegate Task Process<TMsg>(Mailbox<TMsg> mailbox, CancellationToken cancellationToken);
public delegate Task<TState> MessageHandler<TMsg,TState>(TMsg msg, TState state, CancellationToken cancellationToken);
public delegate Task<Unit> MessageHandler<TMsg>(TMsg msg, CancellationToken cancellationToken);

public sealed class MailboxProcessor<TMsg>(
	Process<TMsg> body,
	CancellationToken? cancellationToken = null) : IDisposable
{
	private readonly Mailbox<TMsg> _mailbox = new();

	private readonly Process<TMsg> _body =
		body ?? throw new ArgumentNullException(nameof(body));

	private readonly CancellationTokenSource _cts = cancellationToken != null
		? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value)
		: new CancellationTokenSource();

	private Task? _processingTask;
	private bool _isDisposed;
	public CancellationToken CancellationToken => _cts.Token;

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

	public static MailboxProcessor<TMsg> Spawn<TMsg>(string name, Process<TMsg> body, CancellationToken? cancellationToken = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
		ArgumentNullException.ThrowIfNull(body, nameof(body));

		if (Processors.ContainsKey(name))
		{
			throw new ArgumentException($"A worker named '{name}' already exists.", nameof(name));
		}

		var processor = new MailboxProcessor<TMsg>(body, cancellationToken);
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

	public static Process<Unit> Continuously(
		MessageHandler<Unit> handler) =>
		async (mailbox, cancellationToken) =>
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					_ = await handler(Unit.Instance, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e) when (e is not OperationCanceledException oce ||
				                          oce.CancellationToken != cancellationToken)
				{
					Logger.LogError(e);
				}
			}
		};

	public static Process<TMsg> EventDriven<TMsg,TState>(TState state,
		MessageHandler<TMsg, TState> handler) =>
		async (mailbox, cancellationToken) =>
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var msg = await mailbox.ReceiveAsync(cancellationToken).ConfigureAwait(false);
					state = await handler(msg, state, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e) when(e is not ChannelClosedException)
				{
					Logger.LogError(e);
				}
			}
		};

	public static Process<TMsg> Periodically<TMsg,TState>(TimeSpan period, TState state,
		MessageHandler<TMsg, TState> handler) =>
		async (inbox, cancellationToken) =>
		{
			var lastUpdateTime = DateTime.MinValue;
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var msg = await inbox.ReceiveAsync(cancellationToken).ConfigureAwait(false);
					if (DateTime.UtcNow - lastUpdateTime > period)
					{
						state = await handler(msg, state, cancellationToken).ConfigureAwait(false);
						lastUpdateTime = DateTime.UtcNow;
					}
				}
				catch (Exception e) when(e is not ChannelClosedException)
				{
					Logger.LogError(e);
				}
			}
		};

	public static Process<TMsg> Service<TMsg>(string serviceName, Process<TMsg> process) =>
		Service(
			before: () => Logger.LogInfo($"Starting {serviceName}."),
			process,
			after: () => Logger.LogInfo($"Stopped {serviceName}."));

	public static Process<TMsg> Service<TMsg>(
		Action before,
		Process<TMsg> handler,
		Action after) =>
		async (mailbox, cancellationToken) =>
		{
			before();
			try
			{
				await handler(mailbox, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception e) when (e is not (ChannelClosedException or TaskCanceledException))
			{
				Logger.LogError($"Service will stopped because of unexpected exception: {e}");
			}
			finally
			{
				after();
			}
		};
}
