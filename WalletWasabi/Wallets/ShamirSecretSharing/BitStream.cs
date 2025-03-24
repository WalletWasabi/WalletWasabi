namespace WalletWasabi.Wallets.Slip39;

using System;
using System.IO;

class BitStream
{
	private byte[] _buffer;
	private int _writePos;
	private int _readPos;
	private int _lengthInBits;

	public BitStream(byte[] buffer)
	{
		var newBuffer = new byte[buffer.Length];
		Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
		_buffer = newBuffer;
		_readPos = 0;
		_writePos = 0;
		_lengthInBits = buffer.Length * 8;
	}

	public void WriteBit(bool bit)
	{
		EnsureCapacity();
		if (bit)
		{
			_buffer[_writePos / 8] |= (byte)(1 << (8 - (_writePos % 8) - 1));
		}
		_writePos++;
		_lengthInBits++;
	}

	public void WriteBits(ulong data, byte count)
	{
		data <<= (64 - count);
		while (count >= 8)
		{
			var b = (byte)(data >> (64 - 8));
			WriteByte(b);
			data <<= 8;
			count -= 8;
		}

		while (count > 0)
		{
			var bit = data >> (64 - 1);
			WriteBit(bit == 1);
			data <<= 1;
			count--;
		}
	}

	public void WriteByte(byte b)
	{
		EnsureCapacity();

		var remainCount = (_writePos % 8);
		var i = _writePos / 8;
		_buffer[i] |= (byte)(b >> remainCount);

		var written = (8 - remainCount);
		_writePos += written;
		_lengthInBits += written;

		if (remainCount > 0)
		{
			EnsureCapacity();

			_buffer[i + 1] = (byte)(b << (8 - remainCount));
			_writePos += remainCount;
			_lengthInBits += remainCount;
		}
	}

	public bool TryReadBit(out bool bit)
	{
		bit = false;
		if (_readPos == _lengthInBits)
		{
			return false;
		}

		var mask = 1 << (8 - (_readPos % 8) - 1);

		bit = (_buffer[_readPos / 8] & mask) == mask;
		_readPos++;
		return true;
	}

	public bool TryReadBits(int count, out ulong bits)
	{
		var val = 0UL;
		while (count >= 8)
		{
			val <<= 8;
			if (!TryReadByte(out var readedByte))
			{
				bits = 0U;
				return false;
			}
			val |= (ulong)readedByte;
			count -= 8;
		}

		while (count > 0)
		{
			val <<= 1;
			if (TryReadBit(out var bit))
			{
				val |= bit ? 1UL : 0UL;
				count--;
			}
			else
			{
				bits = 0U;
				return false;
			}
		}
		bits = val;
		return true;
	}

	public bool TryReadByte(out byte b)
	{
		b = 0;
		if (_readPos == _lengthInBits)
		{
			return false;
		}

		var i = _readPos / 8;
		var remainCount = _readPos % 8;
		b = (byte)(_buffer[i] << remainCount);

		if (remainCount > 0)
		{
			if (i + 1 == _buffer.Length)
			{
				b = 0;
				return false;
			}
			b |= (byte)(_buffer[i + 1] >> (8 - remainCount));
		}
		_readPos += 8;
		return true;
	}

	public byte[] ToByteArray()
	{
		var arraySize = (_writePos + 7) / 8;
		var byteArray = new byte[arraySize];
		Array.Copy(_buffer, byteArray, arraySize);
		return byteArray;
	}

	public int Available => _lengthInBits - _readPos;

	private void EnsureCapacity()
	{
		if (_writePos / 8 == _buffer.Length)
		{
			Array.Resize(ref _buffer, _buffer.Length + (4 * 1024));
		}
	}
}

class BitStreamReader(BitStream stream)
{
	public BitStreamReader(byte[] buffer)
		: this(new BitStream(buffer))
	{ }

	public ulong Read(int count) =>
		stream.TryReadBits(count, out var value)
			? value
			: throw new EndOfStreamException("There is not more bits to read.");

	public byte ReadUint8(int count) => (byte)Read(count);

	public ushort ReadUint16(int count) => (ushort)Read(count);

	public bool CanRead(int count) => stream.Available >= count;
	public bool EndOdStream => !CanRead(1);
}

class BitStreamWriter(BitStream stream)
{
	public BitStreamWriter()
		: this(new BitStream(new byte[100]))
	{ }

	public void Write(ulong data, int count) =>
		stream.WriteBits(data, (byte) count);

	public byte[] ToByteArray() =>
		stream.ToByteArray();
}
