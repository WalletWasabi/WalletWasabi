namespace Gma.QrCodeNet.Encoding.DataEncodation.InputRecognition;

public static class ModeEncodeCheck
{
	/// <summary>
	/// Encoding.GetEncoding.GetBytes will transform char to 0x3F if that char not belong to current encoding table.
	/// 0x3F is '?'
	/// </summary>
	private const int QuestionMarkChar = 0x3F;

	/// <summary>
	/// Use given encoding to check input string from starting position. If encoding table is suitable solution.
	/// it will return -1. Else it will return failed encoding position.
	/// </summary>
	/// <param name="content">Input string</param>
	/// <param name="encodingName">Encoding name. Check ECI table</param>
	/// <returns>Returns -1 if from starting position to end encoding success. Else returns fail position</returns>
	internal static int TryEncodeEightBitByte(string content, string encodingName, int startingPosition, int contentLength)
	{
		if (string.IsNullOrEmpty(content))
		{
			throw new IndexOutOfRangeException("Input cannot be null or empty.");
		}

		System.Text.Encoding encoding;
		try
		{
			encoding = System.Text.Encoding.GetEncoding(encodingName);
		}
		catch (ArgumentException)
		{
			return startingPosition;
		}

		char[] currentChar = new char[1];
		byte[] bytes;

		for (int index = startingPosition; index < contentLength; index++)
		{
			currentChar[0] = content[index];
			bytes = encoding.GetBytes(currentChar);
			int length = bytes.Length;
			if (currentChar[0] != '?' && length == 1 && bytes[0] == QuestionMarkChar)
			{
				return index;
			}
			else if (length > 1)
			{
				return index;
			}
		}

		for (int index = 0; index < startingPosition; index++)
		{
			currentChar[0] = content[index];
			bytes = encoding.GetBytes(currentChar);
			int length = bytes.Length;
			if (currentChar[0] != '?' && length == 1 && bytes[0] == QuestionMarkChar)
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
