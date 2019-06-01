using System;

namespace Gma.QrCodeNet.Encoding.EncodingRegion
{
	/// <summary>
	/// Embed version information for version larger or equal to 7.
	/// </summary>
	/// <remarks>ISO/IEC 18004:2000 Chapter 8.10 Page 54</remarks>
	internal static class VersionInformation
	{
		private const int S_VIRectangleHeight = 3;
		private const int S_VIRectangleWidth = 6;

		/// <summary>
		/// Embed version information to Matrix
		/// Only for version greater or equal to 7
		/// </summary>
		/// <param name="tsMatrix">Matrix</param>
		/// <param name="version">version number</param>
		internal static void EmbedVersionInformation(this TriStateMatrix tsMatrix, int version)
		{
			if (version < 7)
			{
				return;
			}

			BitList versionInfo = VersionInfoBitList(version);

			int matrixWidth = tsMatrix.Width;
			//1 cell between version info and position stencil
			int shiftLength = QRCodeConstantVariable.PositionStencilWidth + S_VIRectangleHeight + 1;
			//Reverse order input
			int viIndex = S_LengthDataBits + S_LengthECBits - 1;

			for (int viWidth = 0; viWidth < S_VIRectangleWidth; viWidth++)
			{
				for (int viHeight = 0; viHeight < S_VIRectangleHeight; viHeight++)
				{
					bool bit = versionInfo[viIndex];
					viIndex--;
					//Bottom left
					tsMatrix[viWidth, (matrixWidth - shiftLength + viHeight), MatrixStatus.NoMask] = bit;
					//Top right
					tsMatrix[(matrixWidth - shiftLength + viHeight), viWidth, MatrixStatus.NoMask] = bit;
				}
			}
		}

		private const int S_LengthDataBits = 6;
		private const int S_LengthECBits = 12;
		private const int S_VersionBCHPoly = 0x1f25;

		private static BitList VersionInfoBitList(int version)
		{
			BitList result = new BitList
			{
				{ version, S_LengthDataBits },
				{ BCHCalculator.CalculateBCH(version, S_VersionBCHPoly), S_LengthECBits }
			};

			if (result.Count != (S_LengthECBits + S_LengthDataBits))
			{
				throw new Exception("Version Info creation error. Result is not 18 bits");
			}

			return result;
		}
	}
}
