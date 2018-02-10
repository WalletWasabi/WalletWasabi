using System;

namespace MagicalCryptoWallet.Backend.Gcs
{
	// Manages an array of bits allowing making bit-level operations as a big array of bits
	public sealed class BitArray
	{
		private uint[] _buffer;
		private int _length;

		public BitArray()
			: this(new byte[0]) 
		{
		}

		public BitArray(byte[] bytes)
		{
			if (bytes == null)
				throw new ArgumentNullException(nameof(bytes));

			if (bytes.Length > 268435455)
			{
				throw new ArgumentException("Array is too long", nameof(bytes));
			}
			_buffer = new uint[GetArrayLength(bytes.Length, 4)];
			_length = bytes.Length * 8;
			var num = 0;
			var num2 = 0;
			while (bytes.Length - num2 >= 4)
			{
				_buffer[num++] = ((uint)(bytes[num2] & 255) 
				                | (uint)(bytes[num2 + 1] & 255) << 8 
				                | (uint)(bytes[num2 + 2] & 255) << 16 
				                | (uint)(bytes[num2 + 3] & 255) << 24);
				num2 += 4;
			}
			switch (bytes.Length - num2)
			{
				case 1:
					_buffer[num] |= (uint)(bytes[num2] & 255);
					return;
				case 2:
					_buffer[num] |= (uint)(bytes[num2 + 1] & 255) << 8;
					return;
				case 3:
					_buffer[num] = (uint)(bytes[num2 + 2] & 255) << 16;
					break;
				default:
					return;
			}
		}

		public bool this[int index]
		{
			get => GetBit(index);
			set => SetBit(index, value);
		}

		public int Length
		{
			get => _length;
			set
			{
				if (value < 0)
					throw new ArgumentOutOfRangeException(nameof(value));

				var arrayLength = GetArrayLength(value, 32);
				if (arrayLength > _buffer.Length || arrayLength + 256 < _buffer.Length)
				{
					var array = new uint[arrayLength];
					var count = ((arrayLength > _buffer.Length) ? _buffer.Length : arrayLength) * sizeof(uint);
					Buffer.BlockCopy(_buffer, 0, array, 0, count);
					_buffer = array;
				}
				if (value > _length)
				{
					var num = GetArrayLength(_length, 32) - 1;
					var num2 = _length % 32;
					if (num2 > 0)
					{
						_buffer[num] &= (uint)(1 << num2) - 1;
					}
					Array.Clear(_buffer, num + 1, arrayLength - num - 1);
				}
				_length = value;
			}
		}

		public bool GetBit(int index)
		{
			if (index < 0 || index >= _length)
				throw new ArgumentOutOfRangeException(nameof(index));

			return (_buffer[index / 32] & 1 << index % 32) != 0;
		}

		public ulong GetBits(int index, int count)
		{
			var arrIndex = index / 32;
			var bitIndex = index % 32;
			var value = _buffer[arrIndex];

			var mask = (ulong)((1 << count) - 1);
			var ret = (ulong)(value >> bitIndex);

			if (bitIndex + count > 32)
			{
				var rest = count - (32 - bitIndex);
				ret |= (ulong)_buffer[arrIndex + 1] << (count - rest);
			}
			return ret & mask;
		}

		public void SetBit(int index, bool value)
		{
			if (index < 0 || index >= _length)
				throw new ArgumentOutOfRangeException(nameof(index));

			if (value)
			{
				_buffer[index / 32] |= (uint)(1 << index % 32);
			}
			else
			{
				_buffer[index / 32] &= (uint)(~(1 << index % 32));
			}
		}

		public void SetBits(int index, ulong val, int count)
		{
			//TODO: improve this method. This is a very naive approach
			for (var i = 0; i < count; i++)
			{
				SetBit(index+i, (val & (1UL << i)) != 0);
			}
		}

		internal static int GetArrayLength(int n, int div)
		{
			if (n <= 0)
			{
				return 0;
			}
			return (n - 1) / div + 1;
		}

		public void CopyTo(Array array, int index)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));

			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index));

			if (array is int[])
			{
				Array.Copy(_buffer, 0, array, index, GetArrayLength(_length, 32));
				return;
			}
			if (array is byte[])
			{
				var arrayLength = GetArrayLength(_length, 8);
				if (array.Length - index < arrayLength)
					throw new ArgumentOutOfRangeException(nameof(index));
				
				var array2 = (byte[])array;
				for (var i = 0; i < arrayLength; i++)
				{
					array2[index + i] = (byte)(_buffer[i / 4] >> i % 4 * 8 & 255);
				}
				return;
			}

			if (!(array is bool[]))
				throw new ArgumentException("Dest array type is not supported");

			if (array.Length - index < _length)
				throw new ArgumentOutOfRangeException(nameof(index));

			var array3 = (bool[])array;
			for (var j = 0; j < _length; j++)
			{
				array3[index + j] = ((_buffer[j / 32] >> j % 32 & 1) != 0);
			}
		}

		public byte[] ToByteArray()
		{
			var bytes = new byte[GetArrayLength(_length, 8)];
			CopyTo(bytes, 0);
			return bytes;
		}
	}
}
