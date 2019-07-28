namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition
{
	public struct RecognitionStruct
	{
		public string EncodingName { get; private set; }

		public RecognitionStruct(string encodingName)
			: this()
		{
			EncodingName = encodingName;
		}
	}
}
