using System;
using System.Collections;

namespace MagicalCryptoWallet.Backend
{
	/// <summary> Provides a view of an array of bits as a stream of bits. </summary>
	internal class BitStream
	{
		private readonly FastBitArray _buffer;
		private int _position;

		internal int Position
		{
			get => _position;
			set => _position = value;
		}

		public BitStream(FastBitArray bitArray)
		{
			_buffer = bitArray;
			_position = 0;
		}

		public bool ReadBit()
		{
			return _buffer[_position++];
		}

		public ulong ReadBits(int count)
		{
			var ret = _buffer.GetBits(_position, count);
			_position += count;
			return ret;
		}

		public void WriteBit(bool bit)
		{
			if (_buffer.Length == _position)
			{
				_buffer.Length++;
			}

			_buffer[_position++] = bit;
		}

		public void WriteBits(ulong data, byte count)
		{
			if (_buffer.Length < _position + count)
			{
				_buffer.Length = _position + count;
			}
			_buffer.SetBits(_position, data, count);
			_position += count;
		}
	}


	internal class GRCodedStreamWriter
	{
		private readonly BitStream _stream;
		private readonly byte _p;
		private readonly ulong _modP;
		private ulong _lastValue;

		public GRCodedStreamWriter(BitStream stream, byte p)
		{
			_stream = stream;
			_p = p;
			_modP = (1UL << p);
			_lastValue = 0UL;
		}

		public int Write(ulong value)
		{
			var diff = value - _lastValue;

			var remainder = diff & (_modP - 1);
			var quotient = (diff - remainder) >> _p;

			while (quotient > 0)
			{
				_stream.WriteBit(true);
				quotient--;
			}
			_stream.WriteBit(false);
			_stream.WriteBits(remainder, _p);
			_lastValue = value;
			return _stream.Position;
		}
	}


	internal class GRCodedStreamReader
	{
		private readonly BitStream _stream;
		private readonly byte _p;
		private readonly ulong _modP;
		private ulong _lastValue;

		public GRCodedStreamReader(BitStream stream, byte p, ulong lastValue)
		{
			_stream = stream;
			_p = p;
			_modP = (1UL << p);
			_lastValue = lastValue;
		}

		public ulong Read()
		{
			var currentValue = _lastValue + ReadUInt64();
			_lastValue = currentValue;
			return currentValue;
		}

		private ulong ReadUInt64()
		{
			var count = 0UL;
			var bit = _stream.ReadBit();
			while (bit)
			{
				count++;
				bit = _stream.ReadBit();
			}

			var remainder = _stream.ReadBits(_p);
			var value = (count * _modP) + remainder;
			return value;
		}
	}
}
