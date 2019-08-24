namespace Gma.QrCodeNet.Encoding.Versions
{
	internal struct ErrorCorrectionBlocks
	{
		internal int NumErrorCorrectionCodewards { get; private set; }

		internal int NumBlocks { get; private set; }

		internal int ErrorCorrectionCodewordsPerBlock { get; private set; }

		private ErrorCorrectionBlock[] ECBlock { get; }

		internal ErrorCorrectionBlocks(int numErrorCorrectionCodewords, ErrorCorrectionBlock ecBlock)
			: this()
		{
			NumErrorCorrectionCodewards = numErrorCorrectionCodewords;
			ECBlock = new ErrorCorrectionBlock[] { ecBlock };

			Initialize();
		}

		internal ErrorCorrectionBlocks(int numErrorCorrectionCodewords, ErrorCorrectionBlock ecBlock1, ErrorCorrectionBlock ecBlock2)
			: this()
		{
			NumErrorCorrectionCodewards = numErrorCorrectionCodewords;
			ECBlock = new ErrorCorrectionBlock[] { ecBlock1, ecBlock2 };

			Initialize();
		}

		/// <summary>
		/// Get Error Correction Blocks
		/// </summary>
		internal ErrorCorrectionBlock[] GetECBlocks() => ECBlock;

		/// <summary>
		/// Initialize for NumBlocks and ErrorCorrectionCodewordsPerBlock
		/// </summary>
		private void Initialize()
		{
			if (ECBlock is null)
			{
				throw new System.ArgumentNullException($"{nameof(ErrorCorrectionBlocks)} array does not contain any value.");
			}

			NumBlocks = 0;
			int blockLength = ECBlock.Length;
			for (int i = 0; i < blockLength; i++)
			{
				NumBlocks += ECBlock[i].NumErrorCorrectionBlock;
			}

			ErrorCorrectionCodewordsPerBlock = NumErrorCorrectionCodewards / NumBlocks;
		}
	}
}
