using System;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	public static class CharCountIndicatorTable
	{
		/// <remarks>ISO/IEC 18004:2000 Table 3 Page 18</remarks>
		public static int[] GetCharCountIndicatorSet(Mode mode)
		{
			switch(mode)
			{
				case Mode.Numeric:
					return new int[]{10, 12, 14};
				case Mode.Alphanumeric:
					return new int[]{9, 11, 13};
				case Mode.EightBitByte:
					return new int[]{8, 16, 16};
				case Mode.Kanji:
					return new int[]{8, 10, 12};
				default:
					throw new System.InvalidOperationException(string.Format("Unexpected Mode: {0}", mode.ToString()));
			}
			
		} //
		
		
		public static int GetBitCountInCharCountIndicator(Mode mode, int version)
		{
			int[] charCountIndicatorSet = GetCharCountIndicatorSet(mode);
			int versionGroup = GetVersionGroup(version);
			
			return charCountIndicatorSet[versionGroup];
		}
		
		
		/// <summary>
        /// Used to define length of the Character Count Indicator <see cref="GetBitCountInCharCountIndicator"/>
        /// </summary>
        /// <returns>Returns the 0 based index of the row from Chapter 8.4 Data encodation, Table 3 — Number of bits in Character Count Indicator. </returns>
		private static int GetVersionGroup(int version)
        {
        	if (version > 40)
        	{
        		throw new InvalidOperationException(string.Format("Unexpected version: {0}", version));
        	}
            else if (version>= 27)
            {
                return 2;
            }
			else if (version >= 10)
            {
                return 1;
            }
			else if (version > 0)
			{
				return 0;
			}
			else
				throw new InvalidOperationException(string.Format("Unexpected version: {0}", version));

        }
	}
}
