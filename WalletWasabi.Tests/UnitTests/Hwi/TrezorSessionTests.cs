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
	[Fact]
	public async Task ASessionTheBridgeStillKnowsIsAliveAsync()
	{
		using var transport = new ScriptedTransport();
		transport.Responses.Enqueue(TrezorMessage.Empty(TrezorMessageType.Features));
		using var device = new TrezorDevice(transport);

		Assert.True(await device.IsSessionAliveAsync(CancellationToken.None));

		// The probe must not touch the device state.
		Assert.Equal(TrezorMessageType.GetFeatures, Assert.Single(transport.Received).MessageType);
	}

	[Fact]
	public async Task ASessionTheBridgeForgotIsDeadAsync()
	{
		using var transport = new ScriptedTransport(); // Nothing scripted: every call fails as on a forgotten session.
		using var device = new TrezorDevice(transport);

		Assert.False(await device.IsSessionAliveAsync(CancellationToken.None));
	}

	/// <summary>The bridge and the device are untrusted: an answer that does not parse fails like any device error, not with whatever the parser threw.</summary>
	[Fact]
	public async Task AnUnreadableAnswerIsATrezorFailureAsync()
	{
		using var transport = new ScriptedTransport();
		transport.Responses.Enqueue(new TrezorMessage(TrezorMessageType.PublicKey, [0x18])); // root_fingerprint (field 3) cut off before its value.
		using var device = new TrezorDevice(transport);

		await Assert.ThrowsAsync<TrezorException>(() => device.GetMasterFingerprintAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ADisposedDeviceIsDeadWithoutAskingTheBridgeAsync()
	{
		using var transport = new ScriptedTransport();
		using var device = new TrezorDevice(transport); // Disposing twice is fine, the device shrugs off the second.
		device.Dispose();

		Assert.False(await device.IsSessionAliveAsync(CancellationToken.None));
		Assert.Empty(transport.Received);
	}
}
