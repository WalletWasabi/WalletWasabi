using System;

namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition
{
	public static class ModeEncodeCheck
	{
		public static bool IsModeEncodeValid(string encoding, string content)
		{
			return EightBitByteCheck(encoding, content);
		}

		/// <summary>
		/// Encoding.GetEncoding.GetBytes will transform char to 0x3F if that char not belong to current encoding table.
		/// 0x3F is '?'
		/// </summary>
		private const int QUESTION_MARK_CHAR = 0x3F;

		private static bool EightBitByteCheck(string encodingName, string content)
		{
			int tryEncodePos = TryEncodeEightBitByte(content, encodingName, 0, content.Length);
			return tryEncodePos == -1;
		}

		/// <summary>
		/// Use given encoding to check input string from starting position. If encoding table is suitable solution.
		/// it will return -1. Else it will return failed encoding position.
		/// </summary>
		/// <param name="content">input string</param>
		/// <param name="encodingName">encoding name. Check ECI table</param>
		/// <param name="startPos">starting position</param>
		/// <returns>-1 if from starting position to end encoding success. Else return fail position</returns>
		internal static int TryEncodeEightBitByte(string content, string encodingName, int startPos, int contentLength)
		{
			if (string.IsNullOrEmpty(content))
			{
				throw new IndexOutOfRangeException("Input content should not be Null or empty");
			}

			System.Text.Encoding encoding;
			try
			{
				encoding = System.Text.Encoding.GetEncoding(encodingName);
			}
			catch (ArgumentException)
			{
				return startPos;
			}

			char[] currentChar = new char[1];
			byte[] bytes;

			for (int index = startPos; index < contentLength; index++)
			{
				currentChar[0] = content[index];
				bytes = encoding.GetBytes(currentChar);
				int length = bytes.Length;
				if (currentChar[0] != '?' && length == 1 && bytes[0] == QUESTION_MARK_CHAR)
				{
					return index;
				}
				else if (length > 1)
				{
					return index;
				}
			}

			for (int index = 0; index < startPos; index++)
			{
				currentChar[0] = content[index];
				bytes = encoding.GetBytes(currentChar);
				int length = bytes.Length;
				if (currentChar[0] != '?' && length == 1 && bytes[0] == QUESTION_MARK_CHAR)
				{
					return index;
				}
				else if (length > 1)
				{
					return index;
				}
			}

			return -1;
		}
	}
}
