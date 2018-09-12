namespace Gma.QrCodeNet.Encoding.Versions
{
	internal struct ErrorCorrectionBlocks
	{
		internal int NumErrorCorrectionCodewards { get; private set; }

		internal int NumBlocks { get; private set; }

		internal int ErrorCorrectionCodewordsPerBlock { get; private set; }

		private ErrorCorrectionBlock[] _m_ECBlock;

		internal ErrorCorrectionBlocks(int numErrorCorrectionCodewords, ErrorCorrectionBlock ecBlock)
			: this()
		{
			NumErrorCorrectionCodewards = numErrorCorrectionCodewords;
			_m_ECBlock = new ErrorCorrectionBlock[] { ecBlock };

			Initialize();
		}

		internal ErrorCorrectionBlocks(int numErrorCorrectionCodewords, ErrorCorrectionBlock ecBlock1, ErrorCorrectionBlock ecBlock2)
			: this()
		{
			NumErrorCorrectionCodewards = numErrorCorrectionCodewords;
			_m_ECBlock = new ErrorCorrectionBlock[] { ecBlock1, ecBlock2 };

			Initialize();
		}

		/// <summary>
		/// Get Error Correction Blocks
		/// </summary>
		internal ErrorCorrectionBlock[] GetECBlocks() => _m_ECBlock;

		/// <summary>
		/// Initialize for NumBlocks and ErrorCorrectionCodewordsPerBlock
		/// </summary>
		private void Initialize()
		{
			if (_m_ECBlock is null)
				throw new System.ArgumentNullException("ErrorCorrectionBlocks array doesn't contain any value");

			NumBlocks = 0;
			int blockLength = _m_ECBlock.Length;
			for (int i = 0; i < blockLength; i++)
			{
				NumBlocks += _m_ECBlock[i].NumErrorCorrectionBlock;
			}

			ErrorCorrectionCodewordsPerBlock = NumErrorCorrectionCodewards / NumBlocks;
		}
	}
}
