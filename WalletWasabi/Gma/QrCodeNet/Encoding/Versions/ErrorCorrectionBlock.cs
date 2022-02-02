namespace Gma.QrCodeNet.Encoding.Versions;

internal struct ErrorCorrectionBlock
{
	internal ErrorCorrectionBlock(int numErrorCorrectionBlock, int numDataCodewards)
		: this()
	{
		NumErrorCorrectionBlock = numErrorCorrectionBlock;
		NumDataCodewords = numDataCodewards;
	}

	internal int NumErrorCorrectionBlock { get; private set; }

	internal int NumDataCodewords { get; private set; }
}
