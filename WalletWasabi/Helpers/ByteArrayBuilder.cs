using System.Text;

namespace System
{
	public class ByteArrayBuilder
	{
		private byte[] _buffer;
		public int Length { get; set; }

		public ByteArrayBuilder()
		{
			_buffer = new byte[4096];
			Length = 0;
		}

		public ByteArrayBuilder Append(byte b)
		{
			if (GetUnusedBufferLength() == 0)
			{
				_buffer = IncreaseCapacity(_buffer, _buffer.Length * 2);
			}
			_buffer[Length] = b;
			Length++;
			return this;
		}

		public ByteArrayBuilder Append(byte[] buffer)
		{
			var unusedBufferLength = GetUnusedBufferLength();
			if (unusedBufferLength < buffer.Length)
			{
				_buffer = IncreaseCapacity(_buffer, _buffer.Length + buffer.Length);
			}
			Array.Copy(buffer, 0, _buffer, Length, buffer.Length);
			Length += buffer.Length;
			return this;
		}

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
		/// utf8 encoding
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

		private static byte[] IncreaseCapacity(byte[] buffer, int targetLength)
		{
			var result = new byte[targetLength];
			Array.Copy(buffer, result, buffer.Length);
			return result;
		}
	}
}
