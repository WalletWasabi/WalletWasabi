using System;
using System.Collections;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	/// <remarks>ISO/IEC 18004:2000 Chapter 8.4.2 Page 19</remarks>
    internal class NumericEncoder : EncoderBase
    {
        internal NumericEncoder() 
            : base()
        {
        }

        internal override Mode Mode
        {
            get { return Mode.Numeric; }
        }

        internal override BitList GetDataBits(string content)
        {
        	BitList dataBits = new BitList();
        	int contentLength = content.Length;
            for (int i = 0; i < contentLength; i += 3)
            {
                int groupLength = Math.Min(3, contentLength-i);
                int value = GetDigitGroupValue(content, i, groupLength);
                int bitCount = GetBitCountByGroupLength(groupLength);
                dataBits.Add(value, bitCount);
            }

            return dataBits;
        }
        
        
        protected override int GetBitCountInCharCountIndicator(int version)
        {
            return CharCountIndicatorTable.GetBitCountInCharCountIndicator(Mode.Numeric, version);
        }

        private int GetDigitGroupValue(string content, int startIndex, int length)
        {
            int value=0;
            int iThPowerOf10 = 1;
            for (int i = 0 ; i < length; i++)
            {
                int positionFromEnd = startIndex + length - i - 1;
                int digit = content[positionFromEnd] - '0';
                value += digit * iThPowerOf10;
                iThPowerOf10 *= 10;
            }
            return value;
        }
        
        private bool TryGetDigitGroupValue(string content, int startIndex, int length, out int value)
        {
            value=0;
            int iThPowerOf10 = 1;
            for (int i = 0 ; i < length; i++)
            {
                int positionFromEnd = startIndex + length - i - 1;
                int digit = content[positionFromEnd] - '0';
                //If not numeric. 
                if(digit < 0 || digit > 9)
                	return false;
                value += digit * iThPowerOf10;
                iThPowerOf10 *= 10;
            }
            return true;
        }

        protected int GetBitCountByGroupLength(int groupLength)
        {
            switch (groupLength)
            {
                case 0:
                    return 0;
                case 1:
                    return 4;
                case 2:
                    return 7;
                case 3:
                    return 10;
                default:
                    throw new InvalidOperationException("Unexpected group length:" + groupLength.ToString());
            }
        }
    }
}
