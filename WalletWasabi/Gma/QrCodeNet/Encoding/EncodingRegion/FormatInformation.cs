using Gma.QrCodeNet.Encoding.Masking;
using System;

namespace Gma.QrCodeNet.Encoding.EncodingRegion
{
	/// <summary>
	/// 6.9 Format information
	/// The Format Information is a 15 bit sequence containing 5 data bits, with 10 error correction bits calculated using the (15, 5) BCH code.
	/// </summary>
	/// <remarks>ISO/IEC 18004:2000 Chapter 8.9 Page 53</remarks>
	internal static class FormatInformation
	{
		/// <summary>
		/// Embed format information to tristatematrix.
		/// Process combination of create info bits, BCH error correction bits calculation, embed towards matrix.
		/// </summary>
		/// <remarks>ISO/IEC 18004:2000 Chapter 8.9 Page 53</remarks>
		internal static void EmbedFormatInformation(this TriStateMatrix triMatrix, ErrorCorrectionLevel errorLevel, Pattern pattern)
		{
			BitList formatInfo = GetFormatInfoBits(errorLevel, pattern);
			int width = triMatrix.Width;
			for (int index = 0; index < 15; index++)
			{
				MatrixPoint point = PointForInfo1(index);
				bool bit = formatInfo[index];
				triMatrix[point.X, point.Y, MatrixStatus.NoMask] = bit;

				if (index < 7)
				{
					triMatrix[8, width - 1 - index, MatrixStatus.NoMask] = bit;
				}
				else
				{
					triMatrix[width - 8 + (index - 7), 8, MatrixStatus.NoMask] = bit;
				}
			}
		}

		private static MatrixPoint PointForInfo1(int bitsIndex)
		{
			if (bitsIndex <= 7)
			{
				return bitsIndex >= 6 ? new MatrixPoint(bitsIndex + 1, 8)
					: new MatrixPoint(bitsIndex, 8);
			}
			else
			{
				return bitsIndex == 8 ? new MatrixPoint(8, 8 - (bitsIndex - 7))
					: new MatrixPoint(8, 8 - (bitsIndex - 7) - 1);
			}
		}

		/// <summary>
		/// From Appendix C in JISX0510:2004 (p.65).
		/// </summary>
		private const int S_FormatInfoPoly = 0x537;

		/// <summary>
		/// From Appendix C in JISX0510:2004 (p.65).
		/// </summary>
		private const int S_FormatInfoMaskPattern = 0x5412;

		private static BitList GetFormatInfoBits(ErrorCorrectionLevel errorLevel, Pattern pattern)
		{
			int formatInfo = (int)pattern.MaskPatternType;
			//Pattern bits length = 3
			formatInfo |= GetErrorCorrectionIndicatorBits(errorLevel) << 3;

			int bchCode = BCHCalculator.CalculateBCH(formatInfo, S_FormatInfoPoly);
			//bchCode length = 10
			formatInfo = (formatInfo << 10) | bchCode;

			//xor maskPattern
			formatInfo ^= S_FormatInfoMaskPattern;

			BitList resultBits = new BitList
			{
				{ formatInfo, 15 }
			};

			if (resultBits.Count != 15)
			{
				throw new Exception("FormatInfoBits length is not 15");
			}
			else
			{
				return resultBits;
			}
		}

		//According Table 25 — Error correction level indicators
		//Using this bits as enum values would destroy thir order which currently correspond to error correction strength.
		internal static int GetErrorCorrectionIndicatorBits(ErrorCorrectionLevel errorLevel)
		{
			//L 01
			//M 00
			//Q 11
			//H 10
			switch (errorLevel)
			{
				case ErrorCorrectionLevel.H:
					return 0x02;

				case ErrorCorrectionLevel.L:
					return 0x01;

				case ErrorCorrectionLevel.M:
					return 0x00;

				case ErrorCorrectionLevel.Q:
					return 0x03;

				default:
					throw new ArgumentException($"Unsupported error correction level [{errorLevel}]", nameof(errorLevel));
			}
		}
	}
}
