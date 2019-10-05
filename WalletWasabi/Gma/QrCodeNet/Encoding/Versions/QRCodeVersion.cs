using System;

namespace Gma.QrCodeNet.Encoding.Versions
{
	internal struct QRCodeVersion
	{
		internal int VersionNum { get; private set; }

		internal int TotalCodewords { get; private set; }

		internal int DimensionForVersion { get; private set; }

		private ErrorCorrectionBlocks[] ECBlocks { get; }

		internal QRCodeVersion(int versionNum, int totalCodewords, ErrorCorrectionBlocks ecblocksL, ErrorCorrectionBlocks ecblocksM, ErrorCorrectionBlocks ecblocksQ, ErrorCorrectionBlocks ecblocksH)
			: this()
		{
			VersionNum = versionNum;
			TotalCodewords = totalCodewords;
			ECBlocks = new ErrorCorrectionBlocks[] { ecblocksL, ecblocksM, ecblocksQ, ecblocksH };
			DimensionForVersion = 17 + (versionNum * 4);
		}

		internal ErrorCorrectionBlocks GetECBlocksByLevel(ErrorCorrectionLevel eCLevel)
		{
			return eCLevel switch
			{
				ErrorCorrectionLevel.L => ECBlocks[0],
				ErrorCorrectionLevel.M => ECBlocks[1],
				ErrorCorrectionLevel.Q => ECBlocks[2],
				ErrorCorrectionLevel.H => ECBlocks[3],
				_ => throw new ArgumentOutOfRangeException($"Invalid {nameof(ErrorCorrectionLevel)}.")
			};
		}
	}
}
