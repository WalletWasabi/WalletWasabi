using Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition;
using Gma.QrCodeNet.Encoding.Terminate;
using Gma.QrCodeNet.Encoding.Versions;
using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	/// <remarks>ISO/IEC 18004:2000 Chapter 8.1 Page 14
	/// DataEncode is combination of Data analysis and Data encodation step.
	/// Which uses sub functions under several different namespaces</remarks>
	internal static class DataEncode
	{
		internal static EncodationStruct Encode(string content, ErrorCorrectionLevel ecLevel)
		{
			RecognitionStruct recognitionResult = InputRecognise.Recognise(content);
			EncoderBase encoderBase = CreateEncoder(recognitionResult.EncodingName);

			BitList encodeContent = encoderBase.GetDataBits(content);

			int encodeContentLength = encodeContent.Count;

			VersionControlStruct vcStruct =
				VersionControl.InitialSetup(encodeContentLength, ecLevel, recognitionResult.EncodingName);

			BitList dataCodewords = new BitList();
			//Eci header
			if (vcStruct.IsContainECI && !(vcStruct.ECIHeader is null))
			{
				dataCodewords.Add(vcStruct.ECIHeader);
			}
			//Header
			dataCodewords.Add(encoderBase.GetModeIndicator());
			int numLetter = encodeContentLength >> 3;
			dataCodewords.Add(encoderBase.GetCharCountIndicator(numLetter, vcStruct.VersionDetail.Version));
			//Data
			dataCodewords.Add(encodeContent);
			//Terminator Padding
			dataCodewords.TerminateBites(dataCodewords.Count, vcStruct.VersionDetail.NumDataBytes);

			int dataCodewordsCount = dataCodewords.Count;
			if ((dataCodewordsCount & 0x7) != 0)
			{
				throw new ArgumentException("datacodewords is not byte sized.");
			}
			else if (dataCodewordsCount >> 3 != vcStruct.VersionDetail.NumDataBytes)
			{
				throw new ArgumentException("datacodewords num of bytes not equal to NumDataBytes for current version");
			}

			var encStruct = new EncodationStruct(vcStruct) {
				DataCodewords = dataCodewords
			};
			return encStruct;
		}

		internal static EncodationStruct Encode(IEnumerable<byte> content, ErrorCorrectionLevel eclevel)
		{
			EncoderBase encoderBase = CreateEncoder(QRCodeConstantVariable.DefaultEncoding);

			BitList encodeContent = new BitList(content);

			int encodeContentLength = encodeContent.Count;

			VersionControlStruct vcStruct =
				VersionControl.InitialSetup(encodeContentLength, eclevel, QRCodeConstantVariable.DefaultEncoding);

			BitList dataCodewords = new BitList();
			//Eci header
			if (vcStruct.IsContainECI && !(vcStruct.ECIHeader is null))
			{
				dataCodewords.Add(vcStruct.ECIHeader);
			}
			//Header
			dataCodewords.Add(encoderBase.GetModeIndicator());
			int numLetter = encodeContentLength >> 3;
			dataCodewords.Add(encoderBase.GetCharCountIndicator(numLetter, vcStruct.VersionDetail.Version));
			//Data
			dataCodewords.Add(encodeContent);
			//Terminator Padding
			dataCodewords.TerminateBites(dataCodewords.Count, vcStruct.VersionDetail.NumDataBytes);

			int dataCodewordsCount = dataCodewords.Count;
			if ((dataCodewordsCount & 0x7) != 0)
			{
				throw new ArgumentException("datacodewords is not byte sized.");
			}
			else if (dataCodewordsCount >> 3 != vcStruct.VersionDetail.NumDataBytes)
			{
				throw new ArgumentException("datacodewords num of bytes not equal to NumDataBytes for current version");
			}

			var encStruct = new EncodationStruct(vcStruct) {
				DataCodewords = dataCodewords
			};
			return encStruct;
		}

		private static EncoderBase CreateEncoder(string encodingName)
		{
			return new EightBitByteEncoder(encodingName);
		}
	}
}
