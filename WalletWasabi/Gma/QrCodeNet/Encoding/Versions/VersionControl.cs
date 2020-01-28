using Gma.QrCodeNet.Encoding.DataEncodation;
using System;

namespace Gma.QrCodeNet.Encoding.Versions
{
	internal static class VersionControl
	{
		private const int NumBitsModeIndicator = 4;
		private const string DefaultEncoding = QRCodeConstantVariable.DefaultEncoding;

		/// <summary>
		/// Determine which version to use
		/// </summary>
		/// <param name="dataBitsLength">Number of bits for encoded content</param>
		/// <param name="encodingName">Encoding name for EightBitByte</param>
		/// <returns>VersionDetail and ECI</returns>
		internal static VersionControlStruct InitialSetup(int dataBitsLength, ErrorCorrectionLevel level, string encodingName)
		{
			int totalDataBits = dataBitsLength;

			bool containECI = false;

			BitList eciHeader = new BitList();

			if (encodingName != DefaultEncoding && encodingName != QRCodeConstantVariable.UTF8Encoding)
			{
				ECISet eciSet = new ECISet(ECISet.AppendOption.NameToValue);
				int eciValue = eciSet.GetECIValueByName(encodingName);

				totalDataBits += ECISet.NumOfECIHeaderBits(eciValue);
				eciHeader = eciSet.GetECIHeader(encodingName);
				containECI = true;
			}

			// Determine which version group it belong to
			int searchGroup = DynamicSearchIndicator(totalDataBits, level);

			int[] charCountIndicator = CharCountIndicatorTable.GetCharCountIndicatorSet();

			totalDataBits += (NumBitsModeIndicator + charCountIndicator[searchGroup]);

			int lowerSearchBoundary = searchGroup == 0 ? 1 : (VERSION_GROUP[searchGroup - 1] + 1);
			int higherSearchBoundary = VERSION_GROUP[searchGroup];

			// Binary search to find proper version
			int versionNum = BinarySearch(totalDataBits, level, lowerSearchBoundary, higherSearchBoundary);

			VersionControlStruct vcStruct = FillVCStruct(versionNum, level);

			vcStruct.IsContainECI = containECI;

			vcStruct.ECIHeader = eciHeader;

			return vcStruct;
		}

		private static VersionControlStruct FillVCStruct(int versionNum, ErrorCorrectionLevel level)
		{
			if (versionNum < 1 || versionNum > 40)
			{
				throw new InvalidOperationException($"Unexpected version number: {versionNum}");
			}

			VersionControlStruct vcStruct = new VersionControlStruct();

			int version = versionNum;

			QRCodeVersion versionData = VersionTable.GetVersionByNum(versionNum);

			int numTotalBytes = versionData.TotalCodewords;

			ErrorCorrectionBlocks ecBlocks = versionData.GetECBlocksByLevel(level);
			int numDataBytes = numTotalBytes - ecBlocks.NumErrorCorrectionCodewards;
			int numECBlocks = ecBlocks.NumBlocks;

			VersionDetail vcDetail = new VersionDetail(version, numTotalBytes, numDataBytes, numECBlocks);

			vcStruct.VersionDetail = vcDetail;
			return vcStruct;
		}

		private static readonly int[] VERSION_GROUP = new int[] { 9, 26, 40 };

		/// <summary>
		/// Decide which version group it belong to
		/// </summary>
		/// <param name="numBits">number of bits for bitlist where it contain DataBits encode from input content and ECI header</param>
		/// <param name="level">Error correction level</param>
		/// <returns>Version group index for VERSION_GROUP</returns>
		private static int DynamicSearchIndicator(int numBits, ErrorCorrectionLevel level)
		{
			int[] charCountIndicator = CharCountIndicatorTable.GetCharCountIndicatorSet();
			int loopLength = VERSION_GROUP.Length;
			for (int i = 0; i < loopLength; i++)
			{
				int totalBits = numBits + NumBitsModeIndicator + charCountIndicator[i];

				QRCodeVersion version = VersionTable.GetVersionByNum(VERSION_GROUP[i]);
				int numECCodewords = version.GetECBlocksByLevel(level).NumErrorCorrectionCodewards;

				int dataCodewords = version.TotalCodewords - numECCodewords;

				if (totalBits <= dataCodewords * 8)
				{
					return i;
				}
			}

			throw new InputOutOfBoundaryException($"QRCode do not have enough space for {(numBits + NumBitsModeIndicator + charCountIndicator[2])} bits");
		}

		/// <summary>
		/// Use number of data bits(header + eci header + data bits from EncoderBase) to search for proper version to use
		/// between min and max boundary.
		/// Boundary define by DynamicSearchIndicator method.
		/// </summary>
		private static int BinarySearch(int numDataBits, ErrorCorrectionLevel level, int lowerVersionNum, int higherVersionNum)
		{
			int middleVersionNumber;

			while (lowerVersionNum <= higherVersionNum)
			{
				middleVersionNumber = (lowerVersionNum + higherVersionNum) / 2;
				QRCodeVersion version = VersionTable.GetVersionByNum(middleVersionNumber);
				int numECCodewords = version.GetECBlocksByLevel(level).NumErrorCorrectionCodewards;
				int dataCodewords = version.TotalCodewords - numECCodewords;

				if (dataCodewords << 3 == numDataBits)
				{
					return middleVersionNumber;
				}

				if (dataCodewords << 3 > numDataBits)
				{
					higherVersionNum = middleVersionNumber - 1;
				}
				else
				{
					lowerVersionNum = middleVersionNumber + 1;
				}
			}
			return lowerVersionNum;
		}
	}
}
