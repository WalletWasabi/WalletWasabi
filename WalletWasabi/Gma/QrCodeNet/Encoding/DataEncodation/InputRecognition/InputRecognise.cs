using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition
{
	public static class InputRecognise
	{
		public static RecognitionStruct Recognise(string content)
		{
			string encodingName = EightBitByteRecognision(content, 0, content.Length);
			return new RecognitionStruct(encodingName);
		}

		private static string EightBitByteRecognision(string content, int startPos, int contentLength)
		{
			if (string.IsNullOrEmpty(content))
			{
				throw new ArgumentNullException(nameof(content), "Input content is null or empty");
			}

			var eciSets = new ECISet(ECISet.AppendOption.NameToValue);

			Dictionary<string, int> eciSet = eciSets.GetECITable();

			//we will not check for utf8 encoding.
			eciSet.Remove(QRCodeConstantVariable.UTF8Encoding);
			eciSet.Remove(QRCodeConstantVariable.DefaultEncoding);

			int scanPos = startPos;
			//default encoding as priority
			scanPos = ModeEncodeCheck.TryEncodeEightBitByte(content, QRCodeConstantVariable.DefaultEncoding, scanPos, contentLength);
			if (scanPos == -1)
			{
				return QRCodeConstantVariable.DefaultEncoding;
			}

			foreach (KeyValuePair<string, int> kvp in eciSet)
			{
				scanPos = ModeEncodeCheck.TryEncodeEightBitByte(content, kvp.Key, scanPos, contentLength);
				if (scanPos == -1)
				{
					return kvp.Key;
				}
			}

			if (scanPos == -1)
			{
				throw new ArgumentException("foreach Loop check give wrong result.");
			}
			else
			{
				return QRCodeConstantVariable.UTF8Encoding;
			}
		}
	}
}
