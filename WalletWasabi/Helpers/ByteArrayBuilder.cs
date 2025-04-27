using System.Text;

namespace WalletWasabi.Helpers;

public class ByteArrayBuilder
{
	private byte[] _buffer;

	public ByteArrayBuilder() : this(4096)
	{
	}

	public ByteArrayBuilder(int capacity)
	{
		_buffer = new byte[capacity];
		Length = 0;
	}

	public int Length { get; set; }

	public byte[] ToArray()
	{
		var unusedBufferLength = GetUnusedBufferLength();
		if (unusedBufferLength == 0)
		{
			return _buffer;
		}
		var result = new byte[Length];
		Array.Copy(_buffer, result, result.Length);
		return result;
	}

	/// <summary>
	/// UTF8 encoding
	/// </summary>
	public override string ToString()
	{
		return ToString(Encoding.UTF8);
	}

	public string ToString(Encoding encoding)
	{
		if (Length == 0)
		{
			return "";
		}

		return encoding.GetString(ToArray());
	}

	private int GetUnusedBufferLength()
	{
		return _buffer.Length - Length;
	}
}
