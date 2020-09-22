using WalletWasabi.Helpers;

namespace Gma.QrCodeNet.Encoding.Versions
{
	internal struct ErrorCorrectionBlocks
	{
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

		internal int NumErrorCorrectionCodewards { get; private set; }

		internal int NumBlocks { get; private set; }

		internal int ErrorCorrectionCodewordsPerBlock { get; private set; }

		private ErrorCorrectionBlock[] ECBlock { get; }

		/// <summary>
		/// Get Error Correction Blocks
		/// </summary>
		internal ErrorCorrectionBlock[] GetECBlocks() => ECBlock;

		/// <summary>
		/// Initialize for NumBlocks and ErrorCorrectionCodewordsPerBlock
		/// </summary>
		private void Initialize()
		{
			Guard.NotNull(nameof(ECBlock), ECBlock);

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
