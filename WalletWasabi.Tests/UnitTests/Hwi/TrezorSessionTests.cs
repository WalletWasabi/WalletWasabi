using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Trezor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

/// <summary>
/// A device kept across coinjoin rounds has to notice when the bridge forgot its session (the bridge was
/// restarted, or dropped the device after a USB error), so that the wallet acquires it again instead of
/// failing every call on the dead session.
/// </summary>
public class TrezorSessionTests
{
	private class StubTransport : TrezorBridgeTransport
	{
		public StubTransport(Func<TrezorMessage, TrezorMessage> answer)
			: base("http://127.0.0.1:0")
		{
			_answer = answer;
		}

		private readonly Func<TrezorMessage, TrezorMessage> _answer;

		public int Calls { get; private set; }

		public override Task<TrezorMessage> CallAsync(string session, TrezorMessage message, CancellationToken cancellationToken)
		{
			Calls++;
			return Task.FromResult(_answer(message));
		}
	}

	[Fact]
	public async Task ASessionTheBridgeStillKnowsIsAliveAsync()
	{
		using var transport = new StubTransport(message => message.MessageType == TrezorMessageType.GetFeatures
			? TrezorMessage.Empty(TrezorMessageType.Features)
			: throw new TrezorException($"Unexpected {message.MessageType}: the probe must not touch the device state."));
		using var device = new TrezorDevice(transport);

		Assert.True(await device.IsSessionAliveAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ASessionTheBridgeForgotIsDeadAsync()
	{
		using var transport = new StubTransport(_ => throw new TrezorException("Trezor Bridge request 'call/1' failed with status 400: {\"error\": \"session not found\"}"));
		using var device = new TrezorDevice(transport);

		Assert.False(await device.IsSessionAliveAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ADisposedDeviceIsDeadWithoutAskingTheBridgeAsync()
	{
		using var transport = new StubTransport(_ => TrezorMessage.Empty(TrezorMessageType.Features));
		using var device = new TrezorDevice(transport); // Disposing twice is fine, the device shrugs off the second.
		device.Dispose();

		Assert.False(await device.IsSessionAliveAsync(CancellationToken.None));
		Assert.Equal(0, transport.Calls);
	}
}
