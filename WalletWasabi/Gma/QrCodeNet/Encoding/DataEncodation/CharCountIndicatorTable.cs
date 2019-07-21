using System;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	public static class CharCountIndicatorTable
	{
		/// <remarks>ISO/IEC 18004:2000 Table 3 Page 18</remarks>
		public static int[] GetCharCountIndicatorSet()
		{
			return new int[] { 8, 16, 16 };
		} //

		public static int GetBitCountInCharCountIndicator(int version)
		{
			int[] charCountIndicatorSet = GetCharCountIndicatorSet();
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
				throw new InvalidOperationException($"Unexpected version: {version}");
			}
			else if (version >= 27)
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
			{
				throw new InvalidOperationException($"Unexpected version: {version}");
			}
		}
	}
}
