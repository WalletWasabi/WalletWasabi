namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition;

public struct RecognitionStruct
{
	public RecognitionStruct(string encodingName)
		: this()
	{
		EncodingName = encodingName;
	}

	public string EncodingName { get; private set; }
}
