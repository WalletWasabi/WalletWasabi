namespace Gma.QrCodeNet.Encoding.Versions
{
	internal struct QRCodeVersion
	{
		internal int VersionNum { get; private set;}
		
		internal int TotalCodewords { get; private set;}
		
		internal int DimensionForVersion { get; private set;}
		
		private ErrorCorrectionBlocks[] m_ECBlocks;
		
		internal QRCodeVersion(int versionNum, int totalCodewords, ErrorCorrectionBlocks ecblocksL, ErrorCorrectionBlocks ecblocksM, ErrorCorrectionBlocks ecblocksQ, ErrorCorrectionBlocks ecblocksH)
			: this()
		{
			this.VersionNum = versionNum;
			this.TotalCodewords = totalCodewords;
			this.m_ECBlocks = new ErrorCorrectionBlocks[]{ecblocksL, ecblocksM, ecblocksQ, ecblocksH};
			this.DimensionForVersion = 17 + versionNum * 4;
		}
		
		internal ErrorCorrectionBlocks GetECBlocksByLevel(Gma.QrCodeNet.Encoding.ErrorCorrectionLevel ECLevel)
		{
			switch(ECLevel)
			{
				case ErrorCorrectionLevel.L:
					return m_ECBlocks[0];
				case ErrorCorrectionLevel.M:
					return m_ECBlocks[1];
				case ErrorCorrectionLevel.Q:
					return m_ECBlocks[2];
				case ErrorCorrectionLevel.H:
					return m_ECBlocks[3];
				default:
					throw new System.ArgumentOutOfRangeException("Invalide ErrorCorrectionLevel");
			}
		}
	}
}
