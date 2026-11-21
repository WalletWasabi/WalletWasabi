using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Hwi.Trezor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

public class TrezorWireCodecTests
{
	[Fact]
	public void EncodesAnEmptyMessageAsOnePaddedReport()
	{
		// GetFeatures (55) has no payload.
		var reports = TrezorWireCodec.Encode(new TrezorMessage(55, []));

		var report = Assert.Single(reports);
		Assert.Equal("3F2323003700000000" + new string('0', 2 * 55), Convert.ToHexString(report));
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 1)]
	[InlineData(55, 1)] // Fills the first report exactly.
	[InlineData(56, 2)]
	[InlineData(118, 2)] // Fills the second report exactly.
	[InlineData(119, 3)]
	[InlineData(1000, 16)]
	public void RoundTripsAcrossReportBoundaries(int payloadLength, int expectedReports)
	{
		var message = new TrezorMessage(17, Enumerable.Range(0, payloadLength).Select(i => (byte)(i + 1)).ToArray());

		var reports = TrezorWireCodec.Encode(message);

		Assert.Equal(expectedReports, reports.Count);
		Assert.All(reports, report => Assert.Equal(TrezorWireCodec.ReportLength, report.Length));
		Assert.All(reports, report => Assert.Equal((byte)'?', report[0]));

		var queue = new Queue<byte[]>(reports);
		var decoded = TrezorWireCodec.Decode(queue.Dequeue);

		Assert.Equal(message.MessageType, decoded.MessageType);
		Assert.Equal(message.Payload, decoded.Payload);
		Assert.Empty(queue);
	}

	[Fact]
	public void RejectsAReportThatDoesNotStartAMessage()
	{
		// A continuation report where a header is expected: the reply to a request the caller gave up on.
		var continuation = new byte[TrezorWireCodec.ReportLength];
		continuation[0] = (byte)'?';

		var ex = Assert.Throws<InvalidDataException>(() => TrezorWireCodec.Decode(() => continuation));
		Assert.Contains("3F0000", ex.Message);
	}

	[Fact]
	public void RejectsABrokenContinuation()
	{
		var reports = new Queue<byte[]>(TrezorWireCodec.Encode(new TrezorMessage(17, new byte[100])));
		var first = reports.Dequeue();

		Assert.Throws<InvalidDataException>(() => TrezorWireCodec.Decode(new Queue<byte[]>([first, new byte[TrezorWireCodec.ReportLength]]).Dequeue));
		Assert.Throws<InvalidDataException>(() => TrezorWireCodec.Decode(new Queue<byte[]>([first, reports.Dequeue()[..10]]).Dequeue));
	}

	[Fact]
	public void RejectsALengthAboveTheCapBeforeReadingOn()
	{
		var report = TrezorWireCodec.Encode(new TrezorMessage(17, []))[0];
		report[5] = 0xFF; // Length becomes 0xFF000000.
		int reads = 0;

		Assert.Throws<InvalidDataException>(() => TrezorWireCodec.Decode(() =>
		{
			reads++;
			return report;
		}));
		Assert.Equal(1, reads);
	}
}
