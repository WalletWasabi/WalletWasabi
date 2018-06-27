using System;

namespace Gma.QrCodeNet.Encoding.common
{
	internal static class BitListExtensions
	{
		internal static byte[] ToByteArray(this BitList bitList)
		{
			int bitLength = bitList.Count;
			if((bitLength & 0x7) != 0)
				throw new ArgumentException("bitList count % 8 is not equal to zero");
			
			int numByte = bitLength >> 3;
			
			byte[] result = new byte[numByte];
			
			for(int bitIndex = 0; bitIndex < bitLength; bitIndex++)
			{
				int numBitsInLastByte = bitIndex & 0x7;
				
				if(numBitsInLastByte == 0)
					result[bitIndex >> 3] = 0;
				result[bitIndex >> 3] |= (byte)(ToBit(bitList[bitIndex]) << InverseShiftValue(numBitsInLastByte));
			}
			
			return result;
			
		}
		
		internal static BitList ToBitList(byte[] bArray)
		{
			int bLength = bArray.Length;
			BitList result = new BitList();
			for(int bIndex = 0; bIndex < bLength; bIndex++)
			{
				result.Add((int)bArray[bIndex], 8);
			}
			return result;
		}
		
		private static int ToBit(bool bit)
		{
			switch(bit)
			{
				case true:
					return 1;
				case false:
					return 0;
				default:
					throw new ArgumentException("Invalide bit value");
			}
		}
		
		private static int InverseShiftValue(int numBitsInLastByte)
		{
			return 7 - numBitsInLastByte;
		}
		
	}
}
