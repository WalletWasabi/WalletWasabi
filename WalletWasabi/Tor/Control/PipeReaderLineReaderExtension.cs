using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Tor.Control;

/// <summary>
/// This class employs <c>System.IO.Pipelines</c> to correctly read incoming streamed data.
/// </summary>
/// <remarks>Please read the link below to understand common gotchas of that task.</remarks>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines#what-problem-does-systemiopipelines-solve"/>
public static class PipeReaderLineReaderExtension
{
	/// <summary>
	/// Reads a single line ending with <c>\r\n</c> or <c>\n</c> and returns the line without the suffix.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Parses a single message and updates the consumed SequencePosition and examined <see cref="SequencePosition"/> to point
	/// to the start of the trimmed input buffer.
	/// </para>
	/// <para>
	/// The two SequencePosition arguments are updated because <see cref="TryParseLine"/> removes the parsed message
	/// from the input buffer. Generally, when parsing a single message from the buffer, the examined position should be
	/// one of the following:
	/// <list item="bullet">
	/// <item>The end of the message.</item>
	/// <item>The end of the received buffer if no message was found.</item>
	/// </list>
	/// </para>
	/// <para>
	/// The single message case has the most potential for errors. Passing the wrong values to examined can result
	/// in an out of memory exception or an infinite loop. For more information, see the
	/// <see href="https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines#gotchas">gotchas article</see>
	/// </para>
	/// </remarks>
	/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines#read-a-single-message"/>
	/// <exception cref="InvalidDataException">The message is incomplete and there's no more data to process.</exception>
	/// <exception cref="OperationCanceledException"/>
	public static async ValueTask<string> ReadLineAsync(this PipeReader reader, LineEnding lineEnding = LineEnding.CRLF, CancellationToken cancellationToken = default)
	{
		while (true)
		{
			ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
			ReadOnlySequence<byte> buffer = result.Buffer;

			// In the event that no message is parsed successfully, mark consumed
			// as nothing and examined as the entire buffer.
			SequencePosition consumed = buffer.Start;
			SequencePosition examined = buffer.End;

			try
			{
				if (TryParseLine(ref buffer, lineEnding, out ReadOnlySequence<byte> messageBytes))
				{
					string message = messageBytes.GetString(Encoding.ASCII);

					// A single message was successfully parsed so mark the start as the
					// parsed buffer as consumed. TryParseMessage trims the buffer to
					// point to the data after the message was parsed.
					consumed = buffer.Start;

					// Examined is marked the same as consumed here, so the next call
					// to ReadSingleMessageAsync will process the next message if there's
					// one.
					examined = consumed;

					return message;
				}

				// There's no more data to be processed.
				if (result.IsCompleted)
				{
					if (buffer.Length > 0)
					{
						// The message is incomplete and there's no more data to process.
						throw new InvalidDataException("Incomplete message.");
					}

					Logger.LogTrace("No more data to be processed.");
					break;
				}
			}
			finally
			{
				reader.AdvanceTo(consumed, examined);
			}
		}

		throw new InvalidDataException("No more data.");
	}

	/// <summary>Finds the first newline (<c>\r\n</c>) in the buffer.</summary>
	/// <param name="line">Trims that line, excluding the <c>\r\n</c> from the input buffer.</param>
	/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/io/buffers#process-text-data"/>
	private static bool TryParseLine(ref ReadOnlySequence<byte> buffer, LineEnding lineEnding, out ReadOnlySequence<byte> line)
	{
		SequencePosition position = buffer.Start;
		SequencePosition previous = position;
		int index = -1;
		line = default;

		while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> segment))
		{
			ReadOnlySpan<byte> span = segment.Span;

			if (lineEnding == LineEnding.LF)
			{
				// Look for \n in the current segment.
				index = span.IndexOf((byte)'\n');

				if (index != -1)
				{
					// It was found.
					break;
				}
			}
			else
			{
				// Look for \r in the current segment.
				index = span.IndexOf((byte)'\r');

				if (index != -1)
				{
					// Check next segment for \n.
					if (index + 1 >= span.Length)
					{
						SequencePosition next = position;
						if (!buffer.TryGet(ref next, out ReadOnlyMemory<byte> nextSegment))
						{
							// You're at the end of the sequence.
							return false;
						}
						else if (nextSegment.Span[0] == (byte)'\n')
						{
							//  A match was found.
							break;
						}
					}
					// Check the current segment of \n.
					else if (span[index + 1] == (byte)'\n')
					{
						// It was found.
						break;
					}
				}
			}

			previous = position;
		}

		if (index != -1)
		{
			// Get the position just before the \r\n.
			SequencePosition delimiter = buffer.GetPosition(index, previous);

			// Slice the line (excluding \r\n).
			line = buffer.Slice(buffer.Start, delimiter);

			// Slice the buffer to get the remaining data after the line.
			var offset = lineEnding == LineEnding.CRLF ? 2 : 1;
			buffer = buffer.Slice(buffer.GetPosition(offset, delimiter));
			return true;
		}

		return false;
	}

	private static string GetString(in this ReadOnlySequence<byte> payload, Encoding? encoding = null)
	{
		encoding ??= Encoding.UTF8;

		return payload.IsSingleSegment
			? encoding.GetString(payload.FirstSpan)
			: GetStringSlow(payload, encoding);

		static string GetStringSlow(in ReadOnlySequence<byte> payload, Encoding encoding)
		{
			// linearize
			int length = checked((int)payload.Length);
			byte[] oversized = ArrayPool<byte>.Shared.Rent(length);
			try
			{
				payload.CopyTo(oversized);
				return encoding.GetString(oversized, 0, length);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(oversized);
			}
		}
	}

	public enum LineEnding
	{
		CRLF,
		LF
	}
}
