using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding
{
	internal sealed class BitList : IEnumerable<bool>
	{
		private int _m_BitsSize;

		internal BitList()
		{
			_m_BitsSize = 0;
			List = new List<byte>(32);
		}

		internal BitList(IEnumerable<byte> byteArray)
		{
			_m_BitsSize = byteArray.Count();
			List = byteArray.ToList();
		}

        internal List<byte> List { get; }

        public IEnumerator<bool> GetEnumerator()
		{
			int numBytes = _m_BitsSize >> 3;
			int remainder = _m_BitsSize & 0x7;
			byte value;
			for (int index = 0; index < numBytes; index++)
			{
				value = List[index];
				for (int shiftNum = 7; shiftNum >= 0; shiftNum--)
				{
					yield return ((value >> shiftNum) & 1) == 1;
				}
			}
			if (remainder > 0)
			{
				value = List[numBytes];
				for (int index = 0; index < remainder; index++)
				{
					yield return ((value >> (7 - index)) & 1) == 1;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		internal bool this[int index]
		{
			get
			{
				if (index < 0 || index >= _m_BitsSize)
					throw new ArgumentOutOfRangeException("index", "Index out of range");
				int value_Renamed = List[index >> 3] & 0xff;
				return ((value_Renamed >> (7 - (index & 0x7))) & 1) == 1;
			}
		}

		private int ToBit(bool item)
		{
			return item ? 1 : 0;
		}

		internal void Add(bool item)
		{
			int numBitsinLastByte = _m_BitsSize & 0x7;
			//Add one more byte to List when we have no bits in the last byte.
			if (numBitsinLastByte == 0)
				List.Add(0);

			List[_m_BitsSize >> 3] |= (byte)(ToBit(item) << (7 - numBitsinLastByte));
			_m_BitsSize++;
		}

		internal void Add(IEnumerable<bool> items)
		{
			foreach (bool item in items)
			{
				Add(item);
			}
		}

		internal void Add(int value, int bitCount)
		{
			if (bitCount < 0 || bitCount > 32)
				throw new ArgumentOutOfRangeException("bitCount", "bitCount must greater or equal to 0");
			int numBitsLeft = bitCount;

			while (numBitsLeft > 0)
			{
				if ((_m_BitsSize & 0x7) == 0 && numBitsLeft >= 8)
				{
					//Add one more byte to List.
					byte newByte = (byte)((value >> (numBitsLeft - 8)) & 0xFF);
					AppendByte(newByte);
					numBitsLeft -= 8;
				}
				else
				{
					bool bit = ((value >> (numBitsLeft - 1)) & 1) == 1;
					Add(bit);
					numBitsLeft--;
				}
			}
		}

		private void AppendByte(byte item)
		{
			List.Add(item);
			_m_BitsSize += 8;
		}

		internal int Count
		{
			get
			{
				return _m_BitsSize;
			}
		}

		internal int SizeInByte
		{
			get
			{
				return (_m_BitsSize + 7) >> 3;
			}
		}
	}
}
