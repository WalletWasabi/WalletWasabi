using System;
using System.Collections.Generic;
using Gma.QrCodeNet.Encoding.Versions;

namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition
{
	public static class InputRecognise
	{
		public static RecognitionStruct Recognise(string content)
		{
			int contentLength = content.Length;

			int tryEncodePos = ModeEncodeCheck.TryEncodeKanji(content, contentLength);

			if (tryEncodePos == -2)
				return new RecognitionStruct(Mode.EightBitByte, QRCodeConstantVariable.UTF8Encoding);
			else if (tryEncodePos == -1)
				return new RecognitionStruct(Mode.Kanji, QRCodeConstantVariable.DefaultEncoding);

			tryEncodePos = ModeEncodeCheck.TryEncodeAlphaNum(content, 0, contentLength);

			if (tryEncodePos == -2)
				return new RecognitionStruct(Mode.Numeric, QRCodeConstantVariable.DefaultEncoding);
			else if (tryEncodePos == -1)
				return new RecognitionStruct(Mode.Alphanumeric, QRCodeConstantVariable.DefaultEncoding);

			string encodingName = EightBitByteRecognision(content, tryEncodePos, contentLength);
			return new RecognitionStruct(Mode.EightBitByte, encodingName);
		}

		private static string EightBitByteRecognision(string content, int startPos, int contentLength)
		{
			if (string.IsNullOrEmpty(content))
				throw new ArgumentNullException(nameof(content), "Input content is null or empty");

			var eciSets = new ECISet(ECISet.AppendOption.NameToValue);

			Dictionary<string, int> eciSet = eciSets.GetECITable();

			//we will not check for utf8 encoding.
			eciSet.Remove(QRCodeConstantVariable.UTF8Encoding);
			eciSet.Remove(QRCodeConstantVariable.DefaultEncoding);

			int scanPos = startPos;
			//default encoding as priority
			scanPos = ModeEncodeCheck.TryEncodeEightBitByte(content, QRCodeConstantVariable.DefaultEncoding, scanPos, contentLength);
			if (scanPos == -1)
				return QRCodeConstantVariable.DefaultEncoding;

			foreach (KeyValuePair<string, int> kvp in eciSet)
			{
				scanPos = ModeEncodeCheck.TryEncodeEightBitByte(content, kvp.Key, scanPos, contentLength);
				if (scanPos == -1)
				{
					return kvp.Key;
				}
			}

			if (scanPos == -1)
				throw new ArgumentException("foreach Loop check give wrong result.");
			else
				return QRCodeConstantVariable.UTF8Encoding;
		}
	}
}
