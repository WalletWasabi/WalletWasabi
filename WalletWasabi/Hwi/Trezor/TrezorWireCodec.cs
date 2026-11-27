using System.Buffers.Binary;
using System.IO;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>A device message: its type number and its protobuf payload.</summary>
public record TrezorMessage(ushort MessageType, byte[] Payload);

/// <summary>
/// Trezor wire protocol v1: how one message travels over USB in 64-byte reports.
/// The message is prefixed with "##", its type (u16 BE) and its payload length (u32 BE). That stream is cut into
/// pieces of 63 bytes, each sent in a report that starts with "?", the last one padded with zeros.
/// </summary>
public static class TrezorWireCodec
{
	public const int ReportLength = 64;

	/// <summary>The length field comes from the device, so it is capped before anything is allocated for it.</summary>
	public const int MaxPayloadLength = 1024 * 1024;

	private const int HeaderLength = 8; // "##" + type + length
	private const int ChunkLength = ReportLength - 1;

	public static List<byte[]> Encode(TrezorMessage message)
	{
		var stream = new byte[HeaderLength + message.Payload.Length];
		stream[0] = (byte)'#';
		stream[1] = (byte)'#';
		BinaryPrimitives.WriteUInt16BigEndian(stream.AsSpan(2), message.MessageType);
		BinaryPrimitives.WriteUInt32BigEndian(stream.AsSpan(4), (uint)message.Payload.Length);
		message.Payload.CopyTo(stream, HeaderLength);

		var reports = new List<byte[]>();
		for (int offset = 0; offset < stream.Length; offset += ChunkLength)
		{
			var report = new byte[ReportLength];
			report[0] = (byte)'?';
			stream.AsSpan(offset, Math.Min(ChunkLength, stream.Length - offset)).CopyTo(report.AsSpan(1));
			reports.Add(report);
		}
		return reports;
	}

	/// <summary>Reads one message, asking <paramref name="readReport"/> for as many reports as its length field announces.</summary>
	/// <exception cref="InvalidDataException">The reports are not the start or the continuation of a message. The stream cannot be trusted after that.</exception>
	public static TrezorMessage Decode(Func<byte[]> readReport)
	{
		var report = readReport();
		if (report.Length != ReportLength || report[0] != '?' || report[1] != '#' || report[2] != '#')
		{
			throw new InvalidDataException($"Expected the first report of a message, got {Describe(report)}.");
		}

		ushort messageType = BinaryPrimitives.ReadUInt16BigEndian(report.AsSpan(3));
		uint length = BinaryPrimitives.ReadUInt32BigEndian(report.AsSpan(5));
		if (length > MaxPayloadLength)
		{
			throw new InvalidDataException($"Message of type {messageType} announces {length} bytes, more than the {MaxPayloadLength} allowed.");
		}

		var payload = new byte[length];
		int copied = Math.Min(payload.Length, ReportLength - 1 - HeaderLength);
		report.AsSpan(1 + HeaderLength, copied).CopyTo(payload);

		while (copied < payload.Length)
		{
			report = readReport();
			if (report.Length != ReportLength || report[0] != '?')
			{
				throw new InvalidDataException($"Expected a continuation report, got {Describe(report)}.");
			}

			int count = Math.Min(ChunkLength, payload.Length - copied);
			report.AsSpan(1, count).CopyTo(payload.AsSpan(copied));
			copied += count;
		}

		return new TrezorMessage(messageType, payload);
	}

	private static string Describe(byte[] report) =>
		$"{report.Length} bytes starting with {Convert.ToHexString(report.AsSpan(0, Math.Min(9, report.Length)))}";
}
