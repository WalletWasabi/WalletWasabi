using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Trezor;

namespace WalletWasabi.Tests.UnitTests.Hwi;

/// <summary>
/// A bridge transport that answers device calls from a queue, to drive <see cref="TrezorDevice"/> without a device.
/// Once the queue runs dry it answers like a bridge that forgot the session.
/// </summary>
internal class ScriptedTransport : TrezorBridgeTransport
{
	public ScriptedTransport()
		: base("http://127.0.0.1:0")
	{
	}

	public List<TrezorMessage> Received { get; } = new();
	public Queue<TrezorMessage> Responses { get; } = new();

	public override Task<TrezorMessage> CallAsync(string session, TrezorMessage message, CancellationToken cancellationToken)
	{
		Received.Add(message);

		if (!Responses.TryDequeue(out var response))
		{
			throw new TrezorException("session not found");
		}

		return Task.FromResult(response);
	}
}
