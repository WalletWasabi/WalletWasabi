namespace Gma.QrCodeNet.Encoding.Versions
{
	internal struct ErrorCorrectionBlock
	{
		internal int NumErrorCorrectionBlock { get; private set; }

		internal int NumDataCodewords { get; private set; }

		internal ErrorCorrectionBlock(int numErrorCorrectionBlock, int numDataCodewards)
			: this()
		{
			NumErrorCorrectionBlock = numErrorCorrectionBlock;
			NumDataCodewords = numDataCodewards;
		}
	}
}
