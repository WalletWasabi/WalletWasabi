using System.Threading;
using WalletWasabi.Hwi.Trezor;
using Xunit;

namespace WalletWasabi.Tests.AcceptanceTests;

/// <summary>Needs a Trezor Model T or Safe connected over USB, and Trezor Suite closed. No button press is asked for.</summary>
public class TrezorUsbKatas
{
	[Fact]
	public void DeviceAnswersInitializeWithFeatures()
	{
		const ushort Initialize = 0;
		const ushort Features = 17;

		string path = Assert.Single(TrezorUsbTransport.Enumerate());
		using var transport = TrezorUsbTransport.Open(path);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		// Twice: the answer is longer than one report, and the second call shows the first left nothing behind.
		for (int i = 0; i < 2; i++)
		{
			transport.Write(new TrezorMessage(Initialize, []));
			var answer = transport.Read(timeout.Token);

			Assert.Equal(Features, answer.MessageType);
			Assert.True(answer.Payload.Length > TrezorWireCodec.ReportLength);
		}
	}

	[Fact]
	public void ReadWithNothingToReadCanBeCanceled()
	{
		using var transport = TrezorUsbTransport.Open(Assert.Single(TrezorUsbTransport.Enumerate()));
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

		Assert.Throws<OperationCanceledException>(() => transport.Read(timeout.Token));
	}
}
