﻿namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition
{
	public struct RecognitionStruct
	{
		public Mode Mode { get; private set; }

		public string EncodingName { get; private set; }

		public RecognitionStruct(Mode mode, string encodingName)
			: this()
		{
			Mode = mode;
			EncodingName = encodingName;
		}
	}
}
