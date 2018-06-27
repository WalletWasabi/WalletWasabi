using System;
using System.Collections;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	/// <remarks>ISO/IEC 18004:2000 Chapter 8.4.5 Page 23</remarks>
	internal class KanjiEncoder : EncoderBase
	{
		internal KanjiEncoder()
			:base()
		{
		}
		
		internal override Mode Mode
        {
            get { return Mode.Kanji; }
        }

		/// <summary>
		/// Bitcount according to ISO/IEC 18004:2000 Kanji mode Page 25
		/// </summary>
		private const int KANJI_BITCOUNT = 13;
		
		internal override BitList GetDataBits(string content)
        {
			
			byte[] contentBytes = EncodeContent(content);
			int contentLength = base.GetDataLength(content);
			
			return GetDataBitsByByteArray(contentBytes, contentLength);
		}
		
		internal BitList GetDataBitsByByteArray(byte[] encodeContent, int contentLength)
		{
			BitList dataBits = new BitList();
			
			int bytesLength = encodeContent.Length;
			
			if(bytesLength == contentLength*2)
			{
				for(int i = 0; i < bytesLength; i += 2)
				{
					int encoded = ConvertShiftJIS(encodeContent[i], encodeContent[i+1]);
					dataBits.Add(encoded, KANJI_BITCOUNT);	
				}
			}
			else
				throw new ArgumentOutOfRangeException("Each char must be two byte length");
			
			return dataBits;
		}
		
        protected byte[] EncodeContent(string content)
        {
        	byte[] contentBytes;
        	try 
        	{
				contentBytes = System.Text.Encoding.GetEncoding("shift_jis").GetBytes(content);
			} catch (ArgumentException ex) {
				
				throw ex;
			}
        	return contentBytes;
        }
		
		private const int FST_GROUP_LOWER_BOUNDARY = 0x8140;
		private const int FST_GROUP_UPPER_BOUNDARY = 0x9FFC;
		private const int FST_GROUP_SUBTRACT_VALUE = 0x8140;
		
		private const int SEC_GROUP_LOWER_BOUNDARY = 0xE040;
		private const int SEC_GROUP_UPPER_BOUNDARY = 0xEBBF;
		private const int SEC_GROUP_SUBTRACT_VALUE = 0xC140;
		
		
		/// <summary>
		/// Multiply value for Most significant byte.
		/// Chapter 8.4.5 P.24
		/// </summary>
		private const int MULTIPLY_FOR_msb = 0xC0;
		
		/// <remarks>
		/// See Chapter 8.4.5 P.24 Kanji Mode
		/// </remarks>
		private int ConvertShiftJIS(byte FirstByte, byte SecondByte)
		{
			int ShiftJISValue = (FirstByte << 8) + (SecondByte & 0xff);
			int Subtracted = -1;
			if (ShiftJISValue >= FST_GROUP_LOWER_BOUNDARY && ShiftJISValue <= FST_GROUP_UPPER_BOUNDARY)
			{
				Subtracted = ShiftJISValue - FST_GROUP_SUBTRACT_VALUE;
			}
			else if (ShiftJISValue >= SEC_GROUP_LOWER_BOUNDARY && ShiftJISValue <= SEC_GROUP_UPPER_BOUNDARY)
			{
				Subtracted = ShiftJISValue - SEC_GROUP_SUBTRACT_VALUE;
			}
			else 
				throw new System.ArgumentOutOfRangeException("Char is not inside acceptable range.");
				
			return ((Subtracted >> 8) * MULTIPLY_FOR_msb) + (Subtracted & 0xFF);
		}
		
        protected override int GetBitCountInCharCountIndicator(int version)
        {
        	return CharCountIndicatorTable.GetBitCountInCharCountIndicator(Mode.Kanji, version);
        }
		
	}
}
